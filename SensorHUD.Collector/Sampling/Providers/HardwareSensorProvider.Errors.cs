using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling.Providers;

internal sealed partial class HardwareSensorProvider
{
    private static TelemetryValue GpuValue(
        string id,
        string name,
        string deviceName,
        string unit,
        double? value)
    {
        return Value(
            id,
            name,
            MetricCategories.Gpu,
            unit,
            value,
            value is null ? $"{name} is not exposed for {deviceName}." : null,
            deviceName);
    }

    private void AddUnavailableGpu(List<TelemetryValue> values, string error)
    {
        AddWithError(
            MetricIds.UsageSuffix,
            "GPU usage",
            MetricUnits.Percent);
        AddWithError(
            MetricIds.TemperatureSuffix,
            "GPU temperature",
            MetricUnits.Celsius);
        AddWithError(
            MetricIds.VramSuffix,
            "VRAM usage",
            MetricUnits.Percent);
        AddWithError(
            MetricIds.VramUsedSuffix,
            "VRAM used",
            MetricUnits.Gigabytes);
        AddWithError(
            MetricIds.VramTotalSuffix,
            "VRAM total",
            MetricUnits.Gigabytes);

        void AddWithError(string idSuffix, string name, string unit)
        {
            TelemetryValue value = GpuValue(
                MetricIds.ForGpu("unavailable", idSuffix),
                name,
                "GPU",
                unit,
                value: null);
            value.Error = AppendError(error, _hardwareAccessError);
            values.Add(value);
        }
    }

    private void AddUnavailableNetwork(List<TelemetryValue> values, string error)
    {
        values.Add(
            UnavailableValue(
                MetricIds.NetworkSend,
                "Send",
                MetricCategories.Network,
                MetricUnits.MegabitsPerSecond,
                error,
                "Network"));
        values.Add(
            UnavailableValue(
                MetricIds.NetworkReceive,
                "Receive",
                MetricCategories.Network,
                MetricUnits.MegabitsPerSecond,
                error,
                "Network"));
    }

    private TelemetryValue UnavailableValue(
        string id,
        string name,
        string category,
        string unit,
        string error,
        string deviceName = "")
    {
        return Value(
            id,
            name,
            category,
            unit,
            value: null,
            error: AppendError(error, _hardwareAccessError),
            deviceName: deviceName);
    }

    private static string? AppendError(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        return string.IsNullOrWhiteSpace(second) ? first : $"{first} {second}";
    }

    private static TelemetryValue Value(
        string id,
        string name,
        string category,
        string unit,
        double? value,
        string? error = null,
        string deviceName = "")
    {
        return new TelemetryValue
        {
            Id = id,
            Name = name,
            Category = category,
            Unit = unit,
            Value = value,
            Error = error,
            DeviceName = deviceName,
        };
    }
}
