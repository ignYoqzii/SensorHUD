using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Maps one detected graphics adapter to per-device registry readings.
/// </summary>
internal static class GpuMetricsReader
{
    private const double MegabytesPerGigabyte = 1024;

    private static readonly string[] CoreLoadNames =
        ["GPU Core", "D3D 3D", "GPU Total"];
    private static readonly string[] CoreTemperatureNames =
        ["GPU Core", "GPU"];
    private static readonly string[] VramLoadNames =
        ["GPU Memory", "Memory"];
    private static readonly string[] VramUsedNames =
        ["GPU Memory Used", "Memory Used"];
    private static readonly string[] VramTotalNames =
        ["GPU Memory Total", "Memory Total"];

    public static void Read(
        IHardware gpu,
        List<ISensor> sensorBuffer,
        ICollection<MetricReading> readings,
        string? hardwareAccessError)
    {
        string deviceId = SensorLookup.StableDeviceId(gpu);
        SensorLookup.BufferAll(gpu, sensorBuffer);

        double? usage = Find(SensorType.Load, CoreLoadNames);
        double? temperature = Find(
            SensorType.Temperature,
            CoreTemperatureNames);
        double? vramUsage = Find(SensorType.Load, VramLoadNames);
        double? usedMegabytes = Find(
            SensorType.SmallData,
            VramUsedNames);
        double? totalMegabytes = Find(
            SensorType.SmallData,
            VramTotalNames);

        if (vramUsage is null &&
            usedMegabytes is not null &&
            totalMegabytes > 0)
        {
            vramUsage = usedMegabytes / totalMegabytes * 100;
        }

        Add(MetricRegistry.GpuUsage, usage, "GPU usage");
        Add(
            MetricRegistry.GpuTemperature,
            temperature,
            "GPU temperature");
        Add(MetricRegistry.GpuVramUsage, vramUsage, "VRAM usage");
        Add(
            MetricRegistry.GpuVramUsed,
            usedMegabytes / MegabytesPerGigabyte,
            "VRAM used");
        Add(
            MetricRegistry.GpuVramTotal,
            totalMegabytes / MegabytesPerGigabyte,
            "VRAM total");

        double? Find(SensorType type, string[] names) =>
            SensorLookup.FirstValue(sensorBuffer, type, names);

        void Add(string metricId, double? value, string label)
        {
            readings.Add(value is null
                ? HardwareReading.Unavailable(
                    metricId,
                    gpu.Name,
                    $"{label} is not exposed for {gpu.Name}.",
                    hardwareAccessError,
                    deviceId)
                : HardwareReading.Value(
                    metricId,
                    value,
                    gpu.Name,
                    deviceId));
        }
    }
}
