using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using SensorHUD.Collector.Transport;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Frames;

/// <summary>
/// Owns the DXG ETW session, presentation retention, process selection,
/// process-name cache, and frame-capture status. The provider is filtered to
/// presentation events at the ETW source to minimize graphics-workload cost.
/// </summary>
internal sealed class PresentEventMonitor : IDisposable
{
    private static readonly double RetentionWindowSeconds =
        FrameCaptureDefaults.RetentionWindow.TotalSeconds;

    private readonly Lock _sync = new();
    private readonly Dictionary<int, Queue<double>> _presentsByProcess = [];
    private readonly Dictionary<int, string> _processNameCache = [];
    private readonly List<int> _emptyProcessIds = new(4);
    private readonly Thread _processingThread;

    private TraceEventSession? _session;
    private FrameCaptureState _traceState = FrameCaptureState.Starting;
    private string? _traceError;
    private int _disposeState;

    public PresentEventMonitor()
    {
        _processingThread = new Thread(ProcessEvents)
        {
            IsBackground = true,
            Name = "SensorHUD frame trace",
        };
        _processingThread.Start();
    }

    /// <summary>
    /// Copies the selected process's calculation window while holding the ETW
    /// state lock for the shortest practical time.
    /// </summary>
    public FrameCaptureWindow Capture()
    {
        lock (_sync)
        {
            double now = UtcSeconds(DateTime.UtcNow);
            double retentionCutoff =
                now - RetentionWindowSeconds;
            double calculationCutoff =
                now - FrameCaptureDefaults.CalculationWindow.TotalSeconds;
            RemoveExpired(retentionCutoff);

            if (_traceState is
                FrameCaptureState.Starting or
                FrameCaptureState.Unavailable)
            {
                return new FrameCaptureWindow(
                    _traceState,
                    null,
                    [],
                    _traceError ?? "Starting frame trace.");
            }

            int? processId = ChooseTargetProcess(
                calculationCutoff,
                out int recentCount);
            if (processId is null)
            {
                return new FrameCaptureWindow(
                    FrameCaptureState.WaitingForProcess,
                    null,
                    [],
                    "No presenting process has been detected yet.");
            }

            string? processName =
                _processNameCache.GetValueOrDefault(processId.Value);
            if (string.IsNullOrWhiteSpace(processName))
            {
                processName = $"PID {processId.Value}";
            }

            double[] timestamps = CopyRecent(
                _presentsByProcess[processId.Value],
                calculationCutoff,
                recentCount);
            return new FrameCaptureWindow(
                timestamps.Length < 2
                    ? FrameCaptureState.WarmingUp
                    : FrameCaptureState.Active,
                processName,
                timestamps,
                timestamps.Length < 2
                    ? "Waiting for enough frame samples."
                    : null);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            Volatile.Read(ref _session)?.Dispose();
        }
        catch
        {
            // The trace thread is still joined below so a failed ETW cleanup
            // cannot skip the remaining shutdown coordination.
        }

        if (Thread.CurrentThread != _processingThread)
        {
            _ = _processingThread.Join(TimeSpan.FromSeconds(2));
        }

        lock (_sync)
        {
            _presentsByProcess.Clear();
            _processNameCache.Clear();
            _emptyProcessIds.Clear();
        }
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
            Volatile.Write(ref _session, session);
            if (IsDisposed)
            {
                return;
            }

            session.Source.Dynamic.All += OnTraceEvent;
            try
            {
                TraceEventProviderOptions options = new()
                {
                    EventIDsToEnable =
                    [
                        FrameCaptureDefaults.DxgKernelPresentEventId,
                    ],
                };
                session.EnableProvider(
                    FrameCaptureDefaults.DxgKernelProvider,
                    TraceEventLevel.Verbose,
                    ulong.MaxValue,
                    options);

                lock (_sync)
                {
                    _traceState = FrameCaptureState.WaitingForProcess;
                    _traceError = null;
                }

                session.Source.Process();
            }
            finally
            {
                session.Source.Dynamic.All -= OnTraceEvent;
            }
        }
        catch (Exception) when (IsDisposed)
        {
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
        finally
        {
            Volatile.Write(ref _session, null);
        }
    }

    private void OnTraceEvent(TraceEvent data)
    {
        if (IsDisposed ||
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
            double cutoff = timestamp - RetentionWindowSeconds;
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }
        }
    }

    private void RemoveExpired(double cutoff)
    {
        _emptyProcessIds.Clear();
        foreach ((int processId, Queue<double> queue) in _presentsByProcess)
        {
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }

            if (queue.Count == 0)
            {
                _emptyProcessIds.Add(processId);
            }
        }

        foreach (int processId in _emptyProcessIds)
        {
            _presentsByProcess.Remove(processId);
            _processNameCache.Remove(processId);
        }
    }

    private int? ChooseTargetProcess(
        double cutoff,
        out int recentCount)
    {
        recentCount = 0;
        int foregroundProcessId = GetForegroundProcessId();
        if (foregroundProcessId > 0 &&
            !IsExcluded(foregroundProcessId) &&
            _presentsByProcess.TryGetValue(
                foregroundProcessId,
                out Queue<double>? foregroundQueue))
        {
            int foregroundCount = CountRecent(
                foregroundQueue,
                cutoff);
            if (foregroundCount >= 3)
            {
                recentCount = foregroundCount;
                return foregroundProcessId;
            }
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

        recentCount = bestProcess is null ? 0 : bestCount;
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

    private static double[] CopyRecent(
        Queue<double> timestamps,
        double cutoff,
        int count)
    {
        if (count == 0)
        {
            return [];
        }

        double[] result = new double[count];
        int index = 0;
        foreach (double timestamp in timestamps)
        {
            if (timestamp >= cutoff)
            {
                result[index++] = timestamp;
            }
        }

        return result;
    }

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
            _traceState = FrameCaptureState.Unavailable;
            _traceError = error;
        }
    }

    private static double UtcSeconds(DateTime timestamp) =>
        (timestamp - DateTime.UnixEpoch).TotalSeconds;

    private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;
}

/// <summary>
/// Immutable ETW capture passed to the frame calculation layer.
/// </summary>
internal readonly record struct FrameCaptureWindow(
    FrameCaptureState State,
    string? TargetProcess,
    double[] PresentationTimestamps,
    string? Error);
