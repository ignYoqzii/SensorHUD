using SensorHUD.Core.Metrics;

namespace SensorHUD.Core.Tests;

public sealed class MetricFormatterTests
{
    [Fact]
    public void MissingPresentationValueUsesPlaceholder()
    {
        MetricDefinition definition =
            MetricRegistry.Get(MetricRegistry.CpuUsage);

        IReadOnlyList<MetricTextPart> parts = MetricFormatter.Format(
            definition,
            value: null,
            deviceName: "CPU",
            overrides: null);

        Assert.Equal(
            "N/A",
            Assert.Single(
                parts,
                part => part.Role == MetricTextRole.Value).Text);
    }

    [Fact]
    public void AvailablePresentationValueUsesRegistryPrecision()
    {
        MetricDefinition definition =
            MetricRegistry.Get(MetricRegistry.NetworkSend);

        IReadOnlyList<MetricTextPart> parts = MetricFormatter.Format(
            definition,
            value: 12.34,
            deviceName: "Network",
            overrides: null);

        Assert.Equal(
            12.34.ToString("F1"),
            Assert.Single(
                parts,
                part => part.Role == MetricTextRole.Value).Text);
    }
}
