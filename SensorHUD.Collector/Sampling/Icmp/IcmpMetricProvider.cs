using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Icmp;

/// <summary>
/// Publishes general Internet-path latency and loss from the independent,
/// bounded ICMP probe loop.
/// </summary>
internal sealed class IcmpMetricProvider :
    IMetricProvider,
    IDisposable
{
    private const string DeviceName = "Internet connection";

    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.Global(MetricRegistry.Ping),
        ProvidedMetricDefinition.Global(MetricRegistry.PacketLoss),
    ];

    private readonly InternetPathProbe _probe = new();

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Sample(IMetricSampleSink sink)
    {
        _probe.StartProbe();
        InternetPathStatistics statistics = _probe.Capture();
        if (statistics.PingMilliseconds is double ping)
        {
            sink.PublishGlobal(
                MetricRegistry.Ping,
                ping,
                DeviceName);
        }

        if (statistics.PacketLossPercent is double packetLoss)
        {
            sink.PublishGlobal(
                MetricRegistry.PacketLoss,
                packetLoss,
                DeviceName);
        }
    }

    public void Dispose() => _probe.Dispose();
}
