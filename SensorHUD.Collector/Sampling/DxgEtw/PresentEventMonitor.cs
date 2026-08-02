using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using SensorHUD.Collector.Transport;

namespace SensorHUD.Collector.Sampling.DxgEtw;

/// <summary>
/// Owns the DXG ETW session, presentation retention, process selection,
/// process-name cache, and frame-capture status. The provider is filtered to
/// presentation events at the ETW source to minimize graphics-workload cost.
/// </summary>
internal sealed class PresentEventMonitor : IFrameCaptureSource, IDisposable
{
    private static readonly double RetentionWindowSeconds =
        FrameCaptureDefaults.RetentionWindow.TotalSeconds;

    private readonly Lock _sync = new();
    private readonly Dictionary<int, Queue<double>> _presentsByProcess = [];
    private readonly Dictionary<int, string> _processNameCache = [];
    private readonly List<int> _emptyProcessIds = new(4);
    private readonly FrameProcessSelector _processSelector;
    private readonly Thread _processingThread;

    private TraceEventSession? _session;
    private FrameCaptureSessionState _traceState =
        FrameCaptureSessionState.Starting;
    private string? _traceError;
    private int _disposeState;

    public PresentEventMonitor()
    {
        _processSelector = new FrameProcessSelector(IsExcluded);
        _processingThread = new Thread(ProcessEvents)
        {
            IsBackground = true,
            Name = "SensorHUD frame trace",
        };
        _processingThread.Start();
    }

    public FrameCaptureSubsystemHealth CaptureHealth
    {
        get
        {
            lock (_sync)
            {
                return new FrameCaptureSubsystemHealth(
                    _traceState == FrameCaptureSessionState.Active,
                    _traceState == FrameCaptureSessionState.Unavailable
                        ? _traceError
                        : null);
            }
        }
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
                FrameCaptureSessionState.Starting or
                FrameCaptureSessionState.Unavailable)
            {
                return FrameCaptureWindow.Empty;
            }

            int? processId = _processSelector.ChooseTargetProcess(
                GetForegroundProcessId(),
                _presentsByProcess,
                calculationCutoff,
                out int recentCount);
            if (processId is null)
            {
                return FrameCaptureWindow.Empty;
            }

            double[] timestamps = CopyRecent(
                _presentsByProcess[processId.Value],
                calculationCutoff,
                recentCount);
            return new FrameCaptureWindow(processId.Value, timestamps);
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
                    _traceState = FrameCaptureSessionState.Active;
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
            lock (_sync)
            {
                if (!IsDisposed &&
                    _traceState == FrameCaptureSessionState.Active)
                {
                    _traceState = FrameCaptureSessionState.Unavailable;
                    _traceError =
                        "Frame capture stopped unexpectedly.";
                }
            }
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
            // A process can be briefly inaccessible while it starts. Do not
            // turn that transient lookup failure into a cached exclusion.
            return true;
        }
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
            _traceState = FrameCaptureSessionState.Unavailable;
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
    int ProcessId,
    double[] PresentationTimestamps)
{
    public static FrameCaptureWindow Empty { get; } = new(0, []);
}

internal enum FrameCaptureSessionState
{
    Starting,
    Active,
    Unavailable,
}
