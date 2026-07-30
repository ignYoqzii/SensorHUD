using LibreHardwareMonitor.Hardware;
using SensorHUD.Collector.Sampling.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Network;

/// <summary>
/// Maps LibreHardwareMonitor network-interface sensors into aggregate adapter
/// send and receive throughput readings.
/// </summary>
internal static class AdapterThroughputMetricsReader
{
    private const double BytesToMegabits = 8d / 1_000_000d;
    private const string DeviceName = "Network";
    private static readonly string[] UploadNames =
        ["Upload Speed", "Upload"];
    private static readonly string[] DownloadNames =
        ["Download Speed", "Download"];

    /// <summary>
    /// Adds aggregate throughput across every detected network interface.
    /// </summary>
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

        double? sent = null;
        double? received = null;
        foreach (IHardware network in networks)
        {
            SensorLookup.BufferAll(network, sensorBuffer);
            double? upload = SensorLookup.FirstValue(
                sensorBuffer,
                SensorType.Throughput,
                UploadNames);
            double? download = SensorLookup.FirstValue(
                sensorBuffer,
                SensorType.Throughput,
                DownloadNames);
            if (upload is not null)
            {
                sent = (sent ?? 0) + upload;
            }

            if (download is not null)
            {
                received = (received ?? 0) + download;
            }
        }

        AddReading(
            readings,
            MetricRegistry.NetworkSend,
            sent * BytesToMegabits,
            "upload",
            hardwareAccessError);
        AddReading(
            readings,
            MetricRegistry.NetworkReceive,
            received * BytesToMegabits,
            "download",
            hardwareAccessError);
    }

    private static void AddReading(
        ICollection<MetricReading> readings,
        string metricId,
        double? value,
        string direction,
        string? hardwareAccessError) =>
        readings.Add(value is null
            ? HardwareReading.Unavailable(
                metricId,
                DeviceName,
                $"No network {direction} throughput sensor was found.",
                hardwareAccessError)
            : HardwareReading.Value(
                metricId,
                value,
                DeviceName));
}
