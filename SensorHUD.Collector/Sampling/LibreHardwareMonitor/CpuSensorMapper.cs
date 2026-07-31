using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.LibreHardwareMonitor;

/// <summary>
/// Owns LibreHardwareMonitor CPU outputs and sensor mapping.
/// </summary>
internal sealed class CpuSensorMapper : ILibreHardwareMonitorSensorMapper
{
    private static readonly string[] TotalLoadNames = ["CPU Total", "Total"];

    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.Global(MetricRegistry.CpuUsage),
        ProvidedMetricDefinition.Global(MetricRegistry.CpuTemperature),
    ];

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Map(
        LibreHardwareMonitorSnapshot snapshot,
        IMetricSampleSink sink)
    {
        LibreHardwareDeviceSnapshot? cpu = snapshot.Cpu;
        if (cpu is null)
        {
            return;
        }

        double? usage = cpu.FindFirstValue(
            SensorType.Load,
            TotalLoadNames,
            allowTypeFallback: false);
        if (usage is double usageValue)
        {
            sink.PublishGlobal(
                MetricRegistry.CpuUsage,
                usageValue,
                cpu.Name);
        }

        double? temperature = FindTemperature(cpu.Sensors);
        if (temperature is double temperatureValue)
        {
            sink.PublishGlobal(
                MetricRegistry.CpuTemperature,
                temperatureValue,
                cpu.Name);
        }
    }

    private static double? FindTemperature(
        IReadOnlyList<LibreHardwareSensorSnapshot> sensors)
    {
        foreach (LibreHardwareSensorSnapshot sensor in sensors)
        {
            if (sensor.Type == SensorType.Temperature &&
                sensor.Value is double value &&
                sensor.Name.Contains(
                    "Package",
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        double? maximum = null;
        foreach (LibreHardwareSensorSnapshot sensor in sensors)
        {
            if (sensor.Type == SensorType.Temperature &&
                sensor.Value is double value)
            {
                maximum = maximum is null
                    ? value
                    : Math.Max(maximum.Value, value);
            }
        }

        return maximum;
    }
}
