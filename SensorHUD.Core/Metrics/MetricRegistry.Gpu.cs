namespace SensorHUD.Core.Metrics;

public static partial class MetricRegistry
{
    public const string GpuUsage = "gpu.usage";
    public const string GpuTemperature = "gpu.temperature";
    public const string GpuVramUsage = "gpu.vramUsage";
    public const string GpuVramUsed = "gpu.vramUsed";
    public const string GpuVramTotal = "gpu.vramTotal";

    private static MetricDefinition[] CreateGpuDefinitions() =>
        [
        new()
        {
            Id = GpuUsage,
            Category = MetricCategory.Gpu,
            Name = "Usage",
            Unit = "%",
            Format = "GPU {name}: {value}{unit}",
            Decimals = 0,
            TextColor = "#FF2BFF00",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 0,
            Scope = MetricScope.PerDevice,
        },
        new()
        {
            Id = GpuTemperature,
            Category = MetricCategory.Gpu,
            Name = "Temperature",
            Unit = "°C",
            Format = "GPU Temp: {value}{unit}",
            Decimals = 0,
            TextColor = "#FF2BFF00",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 1,
            Scope = MetricScope.PerDevice,
        },
        new()
        {
            Id = GpuVramUsage,
            Category = MetricCategory.Gpu,
            Name = "VRAM Usage",
            Unit = "%",
            Format = "{device} VRAM: {value}{unit}",
            Decimals = 0,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            IsVisibleByDefault = false,
            SortOrder = 2,
            Scope = MetricScope.PerDevice,
        },
        new()
        {
            Id = GpuVramUsed,
            Category = MetricCategory.Gpu,
            Name = "VRAM Used",
            Unit = "GB",
            Format = "{device} VRAM Used: {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            IsVisibleByDefault = false,
            SortOrder = 3,
            Scope = MetricScope.PerDevice,
        },
        new()
        {
            Id = GpuVramTotal,
            Category = MetricCategory.Gpu,
            Name = "VRAM Total",
            Unit = "GB",
            Format = "{device} VRAM Total: {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            IsVisibleByDefault = false,
            SortOrder = 4,
            Scope = MetricScope.PerDevice,
        },
        ];
}
