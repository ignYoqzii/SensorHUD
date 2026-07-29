using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Frames;

/// <summary>
/// Converts ETW presentation windows and pure calculations into registry
/// readings and typed collector health.
/// </summary>
internal sealed class FrameMetricsProvider :
    ITelemetryProvider,
    IDisposable
{
    private readonly PresentEventMonitor _monitor = new();

    public string Name => "Frame capture";

    public FrameProviderStatus Status { get; private set; } =
        new(FrameCaptureState.Starting);

    public IReadOnlyList<MetricReading> Sample()
    {
        FrameCaptureWindow capture = _monitor.Capture();
        Status = new FrameProviderStatus(
            capture.State,
            capture.TargetProcess,
            capture.State == FrameCaptureState.Unavailable
                ? capture.Error
                : null);

        if (!FrameStatistics.TryCalculate(
                capture.PresentationTimestamps,
                out FrameStatisticsResult statistics))
        {
            return Unavailable(
                capture.Error ?? "Waiting for valid frame intervals.");
        }

        Status = new FrameProviderStatus(
            FrameCaptureState.Active,
            capture.TargetProcess);
        return
        [
            Reading(MetricRegistry.Fps, statistics.Fps),
            Reading(
                MetricRegistry.OnePercentLow,
                statistics.OnePercentLow),
            Reading(MetricRegistry.Frametime, statistics.Frametime),
        ];
    }

    public void Dispose() => _monitor.Dispose();

    private static IReadOnlyList<MetricReading> Unavailable(string error) =>
    [
        Reading(MetricRegistry.Fps, null, error),
        Reading(MetricRegistry.OnePercentLow, null, error),
        Reading(MetricRegistry.Frametime, null, error),
    ];

    private static MetricReading Reading(
        string metricId,
        double? value,
        string? error = null) => new()
        {
            MetricId = metricId,
            Value = value,
            Error = error,
        };
}

/// <summary>
/// Frame provider state copied into collector health.
/// </summary>
internal readonly record struct FrameProviderStatus(
    FrameCaptureState State,
    string? TargetProcess = null,
    string? Error = null);
