using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Aggregates throughput across network interfaces into send/receive metrics.
/// </summary>
internal static class NetworkMetricsReader
{
    private const double BytesToMegabits = 8d / 1_000_000d;
    private const string DeviceName = "Network";
    private static readonly string[] UploadNames =
        ["Upload Speed", "Upload"];
    private static readonly string[] DownloadNames =
        ["Download Speed", "Download"];

    public static void Read(
        IReadOnlyList<IHardware> networks,
        List<ISensor> sensorBuffer,
        ICollection<MetricReading> readings,
        string? hardwareAccessError)
    {
        if (networks.Count == 0)
        {
            const string error =
                "No network interfaces found by LibreHardwareMonitor.";
            readings.Add(HardwareReading.Unavailable(
                MetricRegistry.NetworkSend,
                DeviceName,
                error,
                hardwareAccessError));
            readings.Add(HardwareReading.Unavailable(
                MetricRegistry.NetworkReceive,
                DeviceName,
                error,
                hardwareAccessError));
            return;
        }

        double sent = 0;
        double received = 0;
        foreach (IHardware network in networks)
        {
            SensorLookup.BufferAll(network, sensorBuffer);
            sent += SensorLookup.FirstValue(
                sensorBuffer,
                SensorType.Throughput,
                UploadNames) ?? 0;
            received += SensorLookup.FirstValue(
                sensorBuffer,
                SensorType.Throughput,
                DownloadNames) ?? 0;
        }

        readings.Add(HardwareReading.Value(
            MetricRegistry.NetworkSend,
            sent * BytesToMegabits,
            DeviceName));
        readings.Add(HardwareReading.Value(
            MetricRegistry.NetworkReceive,
            received * BytesToMegabits,
            DeviceName));
    }
}
