namespace SensorHUD.Core.Metrics;

public static partial class MetricRegistry
{
    public const string CpuUsage = "cpu.usage";
    public const string CpuTemperature = "cpu.temperature";

    private static MetricDefinition[] CreateCpuDefinitions() =>
        [
        new()
        {
            Id = CpuUsage,
            Category = MetricCategory.Cpu,
            Name = "Usage",
            Unit = "%",
            Format = "CPU {name}: {value}{unit}",
            Decimals = 0,
            TextColor = "#FFFFE900",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 0,
        },
        new()
        {
            Id = CpuTemperature,
            Category = MetricCategory.Cpu,
            Name = "Temperature",
            Unit = "°C",
            Format = "CPU Temp: {value}{unit}",
            Decimals = 0,
            TextColor = "#FFFFE900",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 1,
        },
        ];
}
