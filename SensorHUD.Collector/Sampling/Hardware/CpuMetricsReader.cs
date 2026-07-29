using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Maps LibreHardwareMonitor CPU sensors to registry metric IDs.
/// </summary>
internal static class CpuMetricsReader
{
    private static readonly string[] TotalLoadNames = ["CPU Total", "Total"];

    public static void Read(
        IHardware? cpu,
        List<ISensor> sensorBuffer,
        ICollection<MetricReading> readings,
        string? hardwareAccessError)
    {
        string deviceName = cpu?.Name ?? "CPU";
        if (cpu is null)
        {
            const string error = "No supported CPU sensor was found.";
            readings.Add(HardwareReading.Unavailable(
                MetricRegistry.CpuUsage,
                deviceName,
                error,
                hardwareAccessError));
            readings.Add(HardwareReading.Unavailable(
                MetricRegistry.CpuTemperature,
                deviceName,
                error,
                hardwareAccessError));
            return;
        }

        SensorLookup.BufferAll(cpu, sensorBuffer);
        double? usage = SensorLookup.FirstValue(
            sensorBuffer,
            SensorType.Load,
            TotalLoadNames);
        readings.Add(usage is null
            ? HardwareReading.Unavailable(
                MetricRegistry.CpuUsage,
                deviceName,
                "CPU load sensors returned no total value.",
                hardwareAccessError)
            : HardwareReading.Value(
                MetricRegistry.CpuUsage,
                usage,
                deviceName));

        double? temperature = FindTemperature(sensorBuffer);
        readings.Add(temperature is null
            ? HardwareReading.Unavailable(
                MetricRegistry.CpuTemperature,
                deviceName,
                "LibreHardwareMonitor returned no CPU temperature value.",
                hardwareAccessError)
            : HardwareReading.Value(
                MetricRegistry.CpuTemperature,
                temperature,
                deviceName));
    }

    private static double? FindTemperature(
        IReadOnlyList<ISensor> sensors)
    {
        ISensor? package = sensors.FirstOrDefault(sensor =>
            sensor.SensorType == SensorType.Temperature &&
            sensor.Value.HasValue &&
            sensor.Name.Contains(
                "Package",
                StringComparison.OrdinalIgnoreCase));
        if (package?.Value is float packageValue)
        {
            return packageValue;
        }

        double? maximum = null;
        foreach (ISensor sensor in sensors)
        {
            if (sensor.SensorType == SensorType.Temperature &&
                sensor.Value is float value)
            {
                maximum = maximum is null
                    ? value
                    : Math.Max(maximum.Value, value);
            }
        }

        return maximum;
    }
}
