using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.DxgEtw;

/// <summary>
/// Converts bounded DXG presentation windows into frame-rate metrics.
/// Capture-subsystem health remains coarse and independent of metric output.
/// </summary>
internal sealed class DxgEtwMetricProvider :
    IMetricProvider,
    IDisposable
{
    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.Global(MetricRegistry.Fps),
        ProvidedMetricDefinition.Global(MetricRegistry.OnePercentLow),
        ProvidedMetricDefinition.Global(MetricRegistry.Frametime),
    ];

    private readonly IFrameCaptureSource _captureSource;
    private readonly TimeProvider _timeProvider;
    private FrameStatisticsResult? _lastStatistics;
    private int _lastProcessId;
    private double _lastPresentationSourceTimestamp = double.NaN;
    private long _lastPresentationObservedTimestamp;

    public DxgEtwMetricProvider()
        : this(new PresentEventMonitor(), TimeProvider.System)
    {
    }

    internal DxgEtwMetricProvider(
        IFrameCaptureSource captureSource,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(captureSource);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _captureSource = captureSource;
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public FrameCaptureSubsystemHealth CaptureHealth =>
        _captureSource.CaptureHealth;

    public void Sample(IMetricSampleSink sink)
    {
        FrameCaptureWindow capture = _captureSource.Capture();
        if (capture.ProcessId != _lastProcessId)
        {
            ResetContinuity(capture.ProcessId);
        }

        if (capture.ProcessId <= 0)
        {
            return;
        }

        if (FrameStatistics.TryCalculate(
                capture.PresentationTimestamps,
                out FrameStatisticsResult statistics))
        {
            _lastStatistics = statistics;
            double latestPresentation =
                GetLatestFiniteTimestamp(
                    capture.PresentationTimestamps);
            if (latestPresentation != _lastPresentationSourceTimestamp)
            {
                _lastPresentationSourceTimestamp = latestPresentation;
                _lastPresentationObservedTimestamp =
                    _timeProvider.GetTimestamp();
            }
        }
        else if (!TryGetRecentStatistics(out statistics))
        {
            return;
        }

        sink.PublishGlobal(MetricRegistry.Fps, statistics.Fps);
        sink.PublishGlobal(
            MetricRegistry.OnePercentLow,
            statistics.OnePercentLow);
        sink.PublishGlobal(
            MetricRegistry.Frametime,
            statistics.Frametime);
    }

    public void Dispose()
    {
        if (_captureSource is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private bool TryGetRecentStatistics(
        out FrameStatisticsResult statistics)
    {
        statistics = default;
        if (_lastStatistics is not FrameStatisticsResult lastStatistics)
        {
            return false;
        }

        TimeSpan age = _timeProvider.GetElapsedTime(
            _lastPresentationObservedTimestamp,
            _timeProvider.GetTimestamp());
        if (age < TimeSpan.Zero ||
            age > FrameCaptureDefaults.ReadingContinuityWindow)
        {
            _lastStatistics = null;
            return false;
        }

        statistics = lastStatistics;
        return true;
    }

    private void ResetContinuity(int processId)
    {
        _lastProcessId = processId;
        _lastStatistics = null;
        _lastPresentationSourceTimestamp = double.NaN;
        _lastPresentationObservedTimestamp = 0;
    }

    private static double GetLatestFiniteTimestamp(
        ReadOnlySpan<double> timestamps)
    {
        for (int index = timestamps.Length - 1; index >= 0; index--)
        {
            if (double.IsFinite(timestamps[index]))
            {
                return timestamps[index];
            }
        }

        // A successful statistics calculation always contains at least one
        // valid interval and therefore at least one finite timestamp.
        throw new InvalidOperationException(
            "The frame window contains no finite presentation timestamp.");
    }
}

/// <summary>
/// Provider-agnostic state of the collector's frame-capture subsystem.
/// </summary>
internal readonly record struct FrameCaptureSubsystemHealth(
    bool IsActive,
    string? Error = null);
