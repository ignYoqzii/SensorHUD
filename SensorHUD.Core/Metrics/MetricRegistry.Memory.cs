namespace SensorHUD.Core.Metrics;

public static partial class MetricRegistry
{
    public const string MemoryUsage = "memory.usage";
    public const string MemoryUsed = "memory.used";
    public const string MemoryTotal = "memory.total";

    private static MetricDefinition[] CreateMemoryDefinitions() =>
        [
        new()
        {
            Id = MemoryUsage,
            Category = MetricCategory.Memory,
            Name = "Usage",
            Unit = "%",
            Format = "{device} Usage: {value}{unit}",
            Decimals = 0,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 0,
        },
        new()
        {
            Id = MemoryUsed,
            Category = MetricCategory.Memory,
            Name = "Used",
            Unit = "GB",
            Format = "{device} Used: {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            IsVisibleByDefault = false,
            SortOrder = 1,
        },
        new()
        {
            Id = MemoryTotal,
            Category = MetricCategory.Memory,
            Name = "Total",
            Unit = "GB",
            Format = "{device} Total: {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            IsVisibleByDefault = false,
            SortOrder = 2,
        },
        ];
}
