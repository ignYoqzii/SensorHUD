using SensorHUD.Collector.Transport;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Reads physical memory totals from Windows, which is more consistent than
/// an optional LibreHardwareMonitor memory device.
/// </summary>
internal static class MemoryMetricsReader
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;
    private const string DeviceName = "System Memory";

    public static void Read(ICollection<MetricReading> readings)
    {
        NativeMethods.MemoryStatus memory =
            NativeMethods.MemoryStatus.Create();
        if (!NativeMethods.GlobalMemoryStatusEx(ref memory) ||
            memory.TotalPhys == 0)
        {
            const string error =
                "Failed to query Windows system memory status.";
            AddUnavailable(
                readings,
                MetricRegistry.MemoryUsage,
                error);
            AddUnavailable(
                readings,
                MetricRegistry.MemoryUsed,
                error);
            AddUnavailable(
                readings,
                MetricRegistry.MemoryTotal,
                error);
            return;
        }

        readings.Add(HardwareReading.Value(
            MetricRegistry.MemoryUsage,
            memory.MemoryLoad,
            DeviceName));
        readings.Add(HardwareReading.Value(
            MetricRegistry.MemoryUsed,
            (memory.TotalPhys - memory.AvailPhys) / BytesPerGigabyte,
            DeviceName));
        readings.Add(HardwareReading.Value(
            MetricRegistry.MemoryTotal,
            memory.TotalPhys / BytesPerGigabyte,
            DeviceName));
    }

    private static void AddUnavailable(
        ICollection<MetricReading> readings,
        string metricId,
        string error) =>
        readings.Add(HardwareReading.Unavailable(
            metricId,
            DeviceName,
            error,
            hardwareAccessError: null));
}
