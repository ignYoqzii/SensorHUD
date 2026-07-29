using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using SensorHUD.Collector.Transport;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Frames;

/// <summary>
/// Owns the ETW session, presentation timestamp retention, foreground-process
/// preference, process-name cache, and capture status.
/// </summary>
internal sealed class PresentEventMonitor : IDisposable
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, Queue<double>> _presentsByProcess = [];
    private readonly Dictionary<int, string> _processNameCache = [];

    private TraceEventSession? _session;
    private string? _traceError = "Starting frame trace…";
    private volatile bool _disposed;

    public PresentEventMonitor()
    {
        _ = Task.Run(ProcessEvents);
    }

    /// <summary>
    /// Copies the current target's calculation window while holding the ETW
    /// state lock for the shortest practical time.
    /// </summary>
    public FrameCaptureWindow Capture()
    {
        lock (_sync)
        {
            double now = UtcSeconds(DateTime.UtcNow);
            double retentionCutoff =
                now - FrameCaptureDefaults.RetentionWindow.TotalSeconds;
            double calculationCutoff =
                now - FrameCaptureDefaults.CalculationWindow.TotalSeconds;
            RemoveExpired(retentionCutoff);

            if (_traceError is not null)
            {
                FrameCaptureState state = _traceError.StartsWith(
                    "Starting",
                    StringComparison.Ordinal)
                    ? FrameCaptureState.Starting
                    : FrameCaptureState.Unavailable;
                return new FrameCaptureWindow(
                    state,
                    null,
                    [],
                    _traceError);
            }

            int? processId = ChooseTargetProcess(calculationCutoff);
            if (processId is null)
            {
                return new FrameCaptureWindow(
                    FrameCaptureState.WaitingForGame,
                    null,
                    [],
                    "No presenting game has been detected yet.");
            }

            string? targetName =
                _processNameCache.GetValueOrDefault(processId.Value);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = $"PID {processId.Value}";
            }

            double[] timestamps = _presentsByProcess[processId.Value]
                .Where(timestamp => timestamp >= calculationCutoff)
                .ToArray();
            return new FrameCaptureWindow(
                timestamps.Length < 2
                    ? FrameCaptureState.WarmingUp
                    : FrameCaptureState.Active,
                targetName,
                timestamps,
                timestamps.Length < 2
                    ? "Waiting for enough frame samples."
                    : null);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _session?.Dispose();
    }

    private void ProcessEvents()
    {
        try
        {
            using TraceEventSession session =
                new($"SensorHUD-Frames-{Environment.ProcessId}")
                {
                    StopOnDispose = true,
                };
            _session = session;
            session.Source.Dynamic.All += OnTraceEvent;
            session.EnableProvider(
                FrameCaptureDefaults.DxgKernelProvider,
                TraceEventLevel.Verbose,
                ulong.MaxValue);

            lock (_sync)
            {
                _traceError = null;
            }

            session.Source.Process();
        }
        catch (UnauthorizedAccessException)
        {
            SetTraceError(
                "Frame telemetry needs ETW permission. Reopen SensorHUD and accept the administrator prompt.");
        }
        catch (Exception exception)
        {
            SetTraceError(
                $"Frame telemetry unavailable: {exception.Message}");
        }
    }

    private void OnTraceEvent(TraceEvent data)
    {
        if (_disposed ||
            data.ProcessID <= 0 ||
            data.ProviderGuid != FrameCaptureDefaults.DxgKernelProvider ||
            (int)data.ID != FrameCaptureDefaults.DxgKernelPresentEventId)
        {
            return;
        }

        double timestamp = UtcSeconds(
            data.TimeStamp.ToUniversalTime());
        lock (_sync)
        {
            if (!_presentsByProcess.TryGetValue(
                    data.ProcessID,
                    out Queue<double>? queue))
            {
                queue = new Queue<double>(128);
                _presentsByProcess.Add(data.ProcessID, queue);
            }

            queue.Enqueue(timestamp);
        }
    }

    private void RemoveExpired(double cutoff)
    {
        foreach ((int processId, Queue<double> queue) in
                 _presentsByProcess.ToArray())
        {
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }

            if (queue.Count == 0)
            {
                _presentsByProcess.Remove(processId);
                _processNameCache.Remove(processId);
            }
        }
    }

    private int? ChooseTargetProcess(double cutoff)
    {
        int foregroundProcessId = GetForegroundProcessId();
        if (foregroundProcessId > 0 &&
            !IsExcluded(foregroundProcessId) &&
            _presentsByProcess.TryGetValue(
                foregroundProcessId,
                out Queue<double>? foregroundQueue) &&
            CountRecent(foregroundQueue, cutoff) >= 3)
        {
            return foregroundProcessId;
        }

        int? bestProcess = null;
        int bestCount = 2;
        foreach ((int processId, Queue<double> queue) in _presentsByProcess)
        {
            if (IsExcluded(processId))
            {
                continue;
            }

            int count = CountRecent(queue, cutoff);
            if (count > bestCount)
            {
                bestCount = count;
                bestProcess = processId;
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

        if (_processNameCache.TryGetValue(
                processId,
                out string? cachedName))
        {
            return FrameCaptureDefaults.ExcludedProcessNames.Contains(
                cachedName);
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            string name = process.ProcessName;
            _processNameCache[processId] = name;
            return FrameCaptureDefaults.ExcludedProcessNames.Contains(name);
        }
        catch
        {
            _processNameCache[processId] = string.Empty;
            return true;
        }
    }

    private static int CountRecent(
        IEnumerable<double> timestamps,
        double cutoff) => timestamps.Count(timestamp =>
            timestamp >= cutoff);

    private static int GetForegroundProcessId()
    {
        nint window = NativeMethods.GetForegroundWindow();
        if (window == 0)
        {
            return 0;
        }

        _ = NativeMethods.GetWindowThreadProcessId(
            window,
            out uint processId);
        return unchecked((int)processId);
    }

    private void SetTraceError(string error)
    {
        lock (_sync)
        {
            _traceError = error;
        }
    }

    private static double UtcSeconds(DateTime timestamp) =>
        (timestamp - DateTime.UnixEpoch).TotalSeconds;
}

/// <summary>
/// Immutable ETW capture passed to the calculation layer.
/// </summary>
internal sealed record FrameCaptureWindow(
    FrameCaptureState State,
    string? TargetProcess,
    double[] PresentationTimestamps,
    string? Error);
