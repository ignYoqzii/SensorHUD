using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Network;

/// <summary>
/// Publishes general Internet-path ping and packet loss independently of any
/// foreground process or application traffic.
/// </summary>
internal sealed class InternetConnectionMetricsProvider :
    ITelemetryProvider,
    IDisposable
{
    private const string DeviceName = "Internet connection";
    private readonly InternetPathProbe _probe = new();

    public string Name => "Internet connection";

    public IReadOnlyList<MetricReading> Sample()
    {
        _probe.StartProbe();
        InternetPathStatistics statistics = _probe.Capture();
        return
        [
            Reading(
                MetricRegistry.Ping,
                statistics.PingMilliseconds,
                statistics.Error),
            Reading(
                MetricRegistry.PacketLoss,
                statistics.PacketLossPercent,
                statistics.Error),
        ];
    }

    public void Dispose() => _probe.Dispose();

    private static MetricReading Reading(
        string metricId,
        double? value,
        string? error) => new()
        {
            MetricId = metricId,
            DeviceName = DeviceName,
            Value = value,
            Error = error,
        };
}
