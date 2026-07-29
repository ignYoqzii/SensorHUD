using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Keeps reading construction and optional hardware-access error composition
/// consistent across focused hardware readers.
/// </summary>
internal static class HardwareReading
{
    public static MetricReading Value(
        string metricId,
        double? value,
        string deviceName,
        string? deviceId = null,
        string? error = null) => new()
        {
            MetricId = metricId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            Value = value,
            Error = error,
        };

    public static MetricReading Unavailable(
        string metricId,
        string deviceName,
        string error,
        string? hardwareAccessError,
        string? deviceId = null) => Value(
            metricId,
            null,
            deviceName,
            deviceId,
            Append(error, hardwareAccessError));

    private static string Append(string first, string? second) =>
        string.IsNullOrWhiteSpace(second) ? first : $"{first} {second}";
}
