using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling.Providers;

internal sealed class FrameMetricsProvider : ITelemetryProvider, IDisposable
{
    private const int DxgKernelPresentEventId = 0x00B8;

    private static readonly Guid DxgKernelProvider = new("802EC45A-1E99-4B83-9920-87C98277BA9D");
    private static readonly TimeSpan CalculationWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromSeconds(6);

    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GameBar",
        "GameBarFTServer",
        "dwm",
        "TextInputHost",
        "ShellExperienceHost",
    };

    private readonly Lock _sync = new();

    // A queue makes expired presentation timestamps inexpensive to remove.
    private readonly Dictionary<int, Queue<double>> _presentsByProcess = [];
    private readonly Dictionary<int, string> _processNameCache = [];

    private TraceEventSession? _session;
    private string? _traceError = "Starting frame trace…";
    private string _status = "Starting frame trace…";
    private volatile bool _disposed;

    public FrameMetricsProvider()
    {
        _ = Task.Run(ProcessEvents);
    }

    /// <summary>
    /// Concise health text copied into collector diagnostics. Access is locked
    /// because ETW callbacks and the sampling loop run on different threads.
    /// </summary>
    public string Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public IReadOnlyList<TelemetryValue> Sample()
    {
        lock (_sync)
        {
            double now = UtcSeconds(DateTime.UtcNow);
            double retentionCutoff = now - RetentionWindow.TotalSeconds;
            double calcCutoff = now - CalculationWindow.TotalSeconds;

            // Drop old presentation timestamps and process-name cache entries.
            foreach (var kvp in _presentsByProcess.ToArray())
            {
                var queue = kvp.Value;
                while (queue.Count > 0 && queue.Peek() < retentionCutoff)
                {
                    queue.Dequeue();
                }
                if (queue.Count == 0)
                {
                    _presentsByProcess.Remove(kvp.Key);
                    _processNameCache.Remove(kvp.Key);
                }
            }

            if (_traceError is not null)
            {
                _status = _traceError;
                return Unavailable(_traceError);
            }

            int? targetProcess = ChooseTargetProcess(calcCutoff);
            if (targetProcess is null)
            {
                _status = "Waiting for a presenting game";
                return Unavailable("No presenting game has been detected yet.");
            }

            string? processName =
                _processNameCache.GetValueOrDefault(targetProcess.Value);
            string targetName = string.IsNullOrWhiteSpace(processName)
                ? $"PID {targetProcess.Value}"
                : processName;
            var targetQueue = _presentsByProcess[targetProcess.Value];

            int count = CountRecentPresents(targetQueue, calcCutoff);

            if (count < 2)
            {
                _status = $"Warming up · {targetName}";
                return Unavailable("Waiting for enough frame samples.");
            }

            Span<double> times = stackalloc double[count];
            int idx = 0;
            foreach (var time in targetQueue)
            {
                if (time >= calcCutoff)
                {
                    times[idx++] = time;
                }
            }

            // Convert consecutive presentation timestamps into milliseconds.
            int intervalCount = count - 1;
            Span<double> frameTimes = stackalloc double[intervalCount];
            int validIntervals = 0;

            for (int i = 0; i < intervalCount; i++)
            {
                double dt = (times[i + 1] - times[i]) * 1000.0;
                if (dt > 0)
                {
                    frameTimes[validIntervals++] = dt;
                }
            }

            if (validIntervals == 0)
            {
                _status = $"Warming up · {targetName}";
                return Unavailable("Waiting for valid frame intervals.");
            }

            Span<double> activeFrameTimes = frameTimes[..validIntervals];

            double duration = times[count - 1] - times[0];
            double fps = duration > 0 ? intervalCount / duration : 0;

            double sum = 0;
            for (int i = 0; i < validIntervals; i++)
            {
                sum += activeFrameTimes[i];
            }

            double frameTime = sum / validIntervals;

            // Compute 1% Low efficiently using a pooled sorting buffer.
            double[] sortedIntervals = ArrayPool<double>.Shared.Rent(validIntervals);
            try
            {
                activeFrameTimes.CopyTo(sortedIntervals);
                Array.Sort(sortedIntervals, 0, validIntervals);

                int slowFrameCount = Math.Max(1, (int)Math.Ceiling(validIntervals * 0.01));
                double slowSum = 0;
                // Values are ascending, so the slowest frames are at the end.
                for (int i = 0; i < slowFrameCount; i++)
                {
                    slowSum += sortedIntervals[validIntervals - 1 - i];
                }

                double slowAverage = slowSum / slowFrameCount;
                double onePercentLow = slowAverage > 0 ? 1000.0 / slowAverage : 0;
                _status = $"Active · {targetName}";

                return
                [
                    Value(MetricIds.Fps, "FPS", MetricUnits.FramesPerSecond, fps),
                    Value(MetricIds.OnePercentLow, "1% Low", MetricUnits.FramesPerSecond, onePercentLow),
                    Value(MetricIds.Frametime, "Frametime", MetricUnits.Milliseconds, frameTime),
                ];
            }
            finally
            {
                ArrayPool<double>.Shared.Return(sortedIntervals);
            }
        }
    }

    private void ProcessEvents()
    {
        try
        {
            string sessionName = $"SensorHUD-Frames-{Environment.ProcessId}";
            using TraceEventSession session = new(sessionName)
            {
                StopOnDispose = true,
            };
            _session = session;

            session.Source.Dynamic.All += OnTraceEvent;
            session.EnableProvider(
                DxgKernelProvider,
                TraceEventLevel.Verbose,
                ulong.MaxValue);

            lock (_sync)
            {
                _traceError = null;
                _status = "Waiting for a presenting game";
            }

            session.Source.Process();
        }
        catch (UnauthorizedAccessException)
        {
            SetTraceError("Frame telemetry needs ETW permission. Reopen SensorHUD and accept the administrator prompt.");
        }
        catch (Exception exception)
        {
            SetTraceError($"Frame telemetry unavailable: {exception.Message}");
        }
    }

    private void OnTraceEvent(TraceEvent data)
    {
        if (_disposed || data.ProcessID <= 0)
        {
            return;
        }

        if (data.ProviderGuid != DxgKernelProvider || (int)data.ID != DxgKernelPresentEventId)
        {
            return;
        }

        double timestamp = UtcSeconds(data.TimeStamp.ToUniversalTime());

        lock (_sync)
        {
            if (!_presentsByProcess.TryGetValue(data.ProcessID, out Queue<double>? queue))
            {
                queue = new Queue<double>(128);
                _presentsByProcess[data.ProcessID] = queue;
            }

            queue.Enqueue(timestamp);
        }
    }

    private int? ChooseTargetProcess(double cutoff)
    {
        int foreground = GetForegroundProcessId();

        if (foreground > 0 && !IsExcluded(foreground))
        {
            if (_presentsByProcess.TryGetValue(foreground, out Queue<double>? fgQueue))
            {
                if (CountRecentPresents(fgQueue, cutoff) >= 3)
                {
                    return foreground;
                }
            }
        }

        int? bestProcess = null;
        int maxCount = 2;

        foreach (var pair in _presentsByProcess)
        {
            if (IsExcluded(pair.Key))
            {
                continue;
            }

            int count = CountRecentPresents(pair.Value, cutoff);
            if (count > maxCount)
            {
                maxCount = count;
                bestProcess = pair.Key;
            }
        }

        return bestProcess;
    }

    private bool IsExcluded(int processId)
    {
        if (processId == Environment.ProcessId)
        {
            return true;
        }

        lock (_sync)
        {
            if (_processNameCache.TryGetValue(processId, out string? cachedName))
            {
                return ExcludedProcessNames.Contains(cachedName);
            }
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            string name = process.ProcessName;

            lock (_sync)
            {
                _processNameCache[processId] = name;
            }

            return ExcludedProcessNames.Contains(name);
        }
        catch
        {
            lock (_sync)
            {
                _processNameCache[processId] = string.Empty;
            }
            return true;
        }
    }

    private static int CountRecentPresents(
        Queue<double> timestamps,
        double cutoff)
    {
        int count = 0;
        foreach (double timestamp in timestamps)
        {
            if (timestamp >= cutoff)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetForegroundProcessId()
    {
        nint window = GetForegroundWindow();
        if (window == 0)
        {
            return 0;
        }

        _ = GetWindowThreadProcessId(window, out uint processId);
        return unchecked((int)processId);
    }

    private void SetTraceError(string message)
    {
        lock (_sync)
        {
            _traceError = message;
            _status = message;
        }
    }

    private static double UtcSeconds(DateTime timestamp)
    {
        return (timestamp - DateTime.UnixEpoch).TotalSeconds;
    }

    private static IReadOnlyList<TelemetryValue> Unavailable(string error)
    {
        return
        [
            Value(MetricIds.Fps, "FPS", MetricUnits.FramesPerSecond, null, error),
            Value(MetricIds.OnePercentLow, "1% Low", MetricUnits.FramesPerSecond, null, error),
            Value(MetricIds.Frametime, "Frametime", MetricUnits.Milliseconds, null, error),
        ];
    }

    private static TelemetryValue Value(string id, string name, string unit, double? value, string? error = null)
    {
        return new TelemetryValue
        {
            Id = id,
            Name = name,
            Category = MetricCategories.FrameRate,
            Unit = unit,
            Value = value,
            Error = error,
        };
    }

    public void Dispose()
    {
        _disposed = true;
        _session?.Dispose();
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(
        nint window,
        out uint processId);
}
