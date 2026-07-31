using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.LibreHardwareMonitor;

/// <summary>
/// Owns aggregate NIC-throughput outputs and LibreHardwareMonitor mapping.
/// </summary>
internal sealed class NicThroughputSensorMapper :
    ILibreHardwareMonitorSensorMapper
{
    private const double BytesToMegabits = 8d / 1_000_000d;
    private const string DeviceName = "Network";
    private static readonly string[] UploadNames =
        ["Upload Speed", "Upload"];
    private static readonly string[] DownloadNames =
        ["Download Speed", "Download"];

    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.Global(MetricRegistry.NetworkSend),
        ProvidedMetricDefinition.Global(MetricRegistry.NetworkReceive),
    ];

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Map(
        LibreHardwareMonitorSnapshot snapshot,
        IMetricSampleSink sink)
    {
        double? sent = null;
        double? received = null;
        foreach (LibreHardwareDeviceSnapshot network in
                 snapshot.NetworkAdapters)
        {
            double? upload = network.FindFirstValue(
                SensorType.Throughput,
                UploadNames,
                allowTypeFallback: false);
            double? download = network.FindFirstValue(
                SensorType.Throughput,
                DownloadNames,
                allowTypeFallback: false);
            if (upload is double uploadValue)
            {
                sent = (sent ?? 0) + uploadValue;
            }

            if (download is double downloadValue)
            {
                received = (received ?? 0) + downloadValue;
            }
        }

        if (sent is double sentValue)
        {
            sink.PublishGlobal(
                MetricRegistry.NetworkSend,
                sentValue * BytesToMegabits,
                DeviceName);
        }

        if (received is double receivedValue)
        {
            sink.PublishGlobal(
                MetricRegistry.NetworkReceive,
                receivedValue * BytesToMegabits,
                DeviceName);
        }
    }
}
