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

        double? usage = SensorLookup.FirstValue(
            sensorBuffer,
            SensorType.Load,
            CoreLoadNames);
        double? temperature = SensorLookup.FirstValue(
            sensorBuffer,
            SensorType.Temperature,
            CoreTemperatureNames);
        double? vramUsage = SensorLookup.FirstValue(
            sensorBuffer,
            SensorType.Load,
            VramLoadNames);
        double? usedMegabytes = SensorLookup.FirstValue(
            sensorBuffer,
            SensorType.SmallData,
            VramUsedNames);
        double? totalMegabytes = SensorLookup.FirstValue(
            sensorBuffer,
            SensorType.SmallData,
            VramTotalNames);

        if (vramUsage is null &&
            usedMegabytes is not null &&
            totalMegabytes > 0)
        {
            vramUsage = usedMegabytes / totalMegabytes * 100;
        }

        AddReading(
            readings,
            MetricRegistry.GpuUsage,
            usage,
            "GPU usage",
            gpu.Name,
            deviceId,
            hardwareAccessError);
        AddReading(
            readings,
            MetricRegistry.GpuTemperature,
            temperature,
            "GPU temperature",
            gpu.Name,
            deviceId,
            hardwareAccessError);
        AddReading(
            readings,
            MetricRegistry.GpuVramUsage,
            vramUsage,
            "VRAM usage",
            gpu.Name,
            deviceId,
            hardwareAccessError);
        AddReading(
            readings,
            MetricRegistry.GpuVramUsed,
            usedMegabytes / MegabytesPerGigabyte,
            "VRAM used",
            gpu.Name,
            deviceId,
            hardwareAccessError);
        AddReading(
            readings,
            MetricRegistry.GpuVramTotal,
            totalMegabytes / MegabytesPerGigabyte,
            "VRAM total",
            gpu.Name,
            deviceId,
            hardwareAccessError);
    }

    private static void AddReading(
        ICollection<MetricReading> readings,
        string metricId,
        double? value,
        string label,
        string deviceName,
        string deviceId,
        string? hardwareAccessError)
    {
        readings.Add(value is null
            ? HardwareReading.Unavailable(
                metricId,
                deviceName,
                $"{label} is not exposed for {deviceName}.",
                hardwareAccessError,
                deviceId)
            : HardwareReading.Value(
                metricId,
                value,
                deviceName,
                deviceId));
    }
}
