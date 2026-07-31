namespace SensorHUD.Core.Metrics;

public static partial class MetricRegistry
{
    public const string Fps = "fps";
    public const string OnePercentLow = "fps.onePercentLow";
    public const string Frametime = "fps.frametime";

    private static MetricDefinition[] CreateFrameRateDefinitions() =>
        [
        new()
        {
            Id = Fps,
            Category = MetricCategory.FrameRate,
            Name = "FPS",
            Unit = "FPS",
            Format = "{name}: {value} {unit}",
            Decimals = 0,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 0,
        },
        new()
        {
            Id = OnePercentLow,
            Category = MetricCategory.FrameRate,
            Name = "1% Low",
            Unit = "FPS",
            Format = "{name}: {value} {unit}",
            Decimals = 0,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 1,
        },
        new()
        {
            Id = Frametime,
            Category = MetricCategory.FrameRate,
            Name = "Frametime",
            Unit = "ms",
            Format = "{name}: {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 2,
        },
        ];
}
