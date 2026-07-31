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

    private readonly PresentEventMonitor _monitor = new();

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public FrameCaptureSubsystemHealth CaptureHealth =>
        _monitor.CaptureHealth;

    public void Sample(IMetricSampleSink sink)
    {
        FrameCaptureWindow capture = _monitor.Capture();
        if (!FrameStatistics.TryCalculate(
                capture.PresentationTimestamps,
                out FrameStatisticsResult statistics))
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

    public void Dispose() => _monitor.Dispose();
}

/// <summary>
/// Provider-agnostic state of the collector's frame-capture subsystem.
/// </summary>
internal readonly record struct FrameCaptureSubsystemHealth(
    bool IsActive,
    string? Error = null);
