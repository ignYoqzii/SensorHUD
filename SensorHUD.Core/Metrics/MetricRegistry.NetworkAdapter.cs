namespace SensorHUD.Core.Metrics;

public static partial class MetricRegistry
{
    public const string NetworkSend = "network.send";
    public const string NetworkReceive = "network.receive";

    private static MetricDefinition[] CreateNetworkAdapterDefinitions() =>
        [
        new()
        {
            Id = NetworkSend,
            Category = MetricCategory.Network,
            Name = "Send",
            Unit = "Mbps",
            Format = "↑ {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 0,
        },
        new()
        {
            Id = NetworkReceive,
            Category = MetricCategory.Network,
            Name = "Receive",
            Unit = "Mbps",
            Format = "↓ {value} {unit}",
            Decimals = 1,
            TextColor = "#FFFFFFFF",
            ValueUnitColor = "#FFFFFFFF",
            SortOrder = 1,
        },
        ];
}
