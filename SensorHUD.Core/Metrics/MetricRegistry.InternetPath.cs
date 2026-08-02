namespace SensorHUD.Core.Metrics;

public static partial class MetricRegistry
{
    public const string Ping = "network.ping";
    public const string PacketLoss = "network.packetLoss";

    private static MetricDefinition[] CreateInternetPathDefinitions() =>
        [
        new()
        {
            Id = Ping,
            Category = MetricCategory.Network,
            Name = "Ping",
            Unit = "ms",
            Format = "{name}: {value} {unit}",
            Decimals = 0,
            TextColor = "#FF118FFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 2,
        },
        new()
        {
            Id = PacketLoss,
            Category = MetricCategory.Network,
            Name = "Packet Loss",
            Unit = "%",
            Format = "{name}: {value}{unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            IsVisibleByDefault = false,
            SortOrder = 3,
        },
        ];
}
