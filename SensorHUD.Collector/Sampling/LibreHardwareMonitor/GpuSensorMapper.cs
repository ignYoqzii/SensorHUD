using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.LibreHardwareMonitor;

/// <summary>
/// Owns all per-device GPU outputs and LibreHardwareMonitor sensor mapping.
/// </summary>
internal sealed class GpuSensorMapper : ILibreHardwareMonitorSensorMapper
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

    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuUsage),
        ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuTemperature),
        ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuVramUsage),
        ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuVramUsed),
        ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuVramTotal),
    ];

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Map(
        LibreHardwareMonitorSnapshot snapshot,
        IMetricSampleSink sink)
    {
        foreach (LibreHardwareDeviceSnapshot gpu in snapshot.Gpus)
        {
            try
            {
                MapDevice(gpu, sink);
            }
            catch
            {
                // A malformed adapter snapshot cannot suppress other GPUs.
            }
        }
    }

    private static void MapDevice(
        LibreHardwareDeviceSnapshot gpu,
        IMetricSampleSink sink)
    {
        foreach (ProvidedMetricDefinition metric in Outputs)
        {
            sink.DeclareDevice(
                metric.MetricId,
                gpu.DeviceId,
                gpu.Name);
        }

        double? usage = gpu.FindFirstValue(
            SensorType.Load,
            CoreLoadNames,
            allowTypeFallback: false);
        double? temperature = gpu.FindFirstValue(
            SensorType.Temperature,
            CoreTemperatureNames);
        double? vramUsage = gpu.FindFirstValue(
            SensorType.Load,
            VramLoadNames,
            allowTypeFallback: false);
        double? usedMegabytes = gpu.FindFirstValue(
            SensorType.SmallData,
            VramUsedNames,
            allowTypeFallback: false);
        double? totalMegabytes = gpu.FindFirstValue(
            SensorType.SmallData,
            VramTotalNames,
            allowTypeFallback: false);

        if (vramUsage is null &&
            usedMegabytes is double used &&
            totalMegabytes is double total &&
            total > 0)
        {
            vramUsage = used / total * 100;
        }

        Publish(
            sink,
            MetricRegistry.GpuUsage,
            gpu,
            usage);
        Publish(
            sink,
            MetricRegistry.GpuTemperature,
            gpu,
            temperature);
        Publish(
            sink,
            MetricRegistry.GpuVramUsage,
            gpu,
            vramUsage);
        Publish(
            sink,
            MetricRegistry.GpuVramUsed,
            gpu,
            usedMegabytes / MegabytesPerGigabyte);
        Publish(
            sink,
            MetricRegistry.GpuVramTotal,
            gpu,
            totalMegabytes / MegabytesPerGigabyte);
    }

    private static void Publish(
        IMetricSampleSink sink,
        string metricId,
        LibreHardwareDeviceSnapshot gpu,
        double? value)
    {
        if (value is double available)
        {
            sink.PublishDevice(
                metricId,
                gpu.DeviceId,
                gpu.Name,
                available);
        }
    }
}
