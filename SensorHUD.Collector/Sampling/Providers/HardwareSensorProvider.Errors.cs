using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling.Providers;

internal sealed partial class HardwareSensorProvider
{
    private static TelemetryValue GpuValue(string id, string name, string deviceName, string unit, double? value)
    {
        return Value(id, name, MetricCategories.Gpu, unit, value, value is null ? $"{name} is not exposed for {deviceName}." : null, deviceName);
    }

    private void AddUnavailableGpu(List<TelemetryValue> values, string error)
    {
        values.Add(WithError(GpuValue(MetricIds.ForGpu("unavailable", MetricIds.UsageSuffix), "GPU usage", "GPU", MetricUnits.Percent, null), error));
        values.Add(WithError(GpuValue(MetricIds.ForGpu("unavailable", MetricIds.TemperatureSuffix), "GPU temperature", "GPU", MetricUnits.Celsius, null), error));
        values.Add(WithError(GpuValue(MetricIds.ForGpu("unavailable", MetricIds.VramSuffix), "VRAM usage", "GPU", MetricUnits.Percent, null), error));

        TelemetryValue WithError(TelemetryValue val, string msg)
        {
            val.Error = AppendError(msg, _hardwareAccessError);
            return val;
        }
    }

    private void AddUnavailableNetwork(List<TelemetryValue> values, string error)
    {
        values.Add(UnavailableValue(MetricIds.NetworkSend, "Send", MetricCategories.Network, MetricUnits.MegabitsPerSecond, error, "Network"));
        values.Add(UnavailableValue(MetricIds.NetworkReceive, "Receive", MetricCategories.Network, MetricUnits.MegabitsPerSecond, error, "Network"));
    }

    private TelemetryValue UnavailableValue(string id, string name, string category, string unit, string error, string deviceName = "")
    {
        return Value(id, name, category, unit, null, AppendError(error, _hardwareAccessError), deviceName);
    }

    private static string? AppendError(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first)) return second;
        return string.IsNullOrWhiteSpace(second) ? first : $"{first} {second}";
    }

    private static TelemetryValue Value(string id, string name, string category, string unit, double? value, string? error = null, string deviceName = "")
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
