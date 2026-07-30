using SensorHUD.Core.Metrics;

namespace SensorHUD.Core.Tests;

public sealed class MetricRegistryTests
{
    [Fact]
    public void DefinitionsHaveUniqueStableIds()
    {
        string[] ids = [.. MetricRegistry.All.Select(metric => metric.Id)];

        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(
            ids.Length,
            ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CategoryQueriesReturnOnlyRequestedScopeInDisplayOrder()
    {
        HashSet<string> indexedMetricIds =
            new(StringComparer.Ordinal);
        foreach (MetricCategoryDefinition category in
                 MetricRegistry.Categories)
        {
            foreach (MetricScope scope in Enum.GetValues<MetricScope>())
            {
                IReadOnlyList<MetricDefinition> metrics =
                    MetricRegistry.GetMetrics(category.Id, scope);

                Assert.All(
                    metrics,
                    metric =>
                    {
                        Assert.Equal(category.Id, metric.Category);
                        Assert.Equal(scope, metric.Scope);
                    });
                Assert.Equal(
                    metrics.OrderBy(metric => metric.SortOrder),
                    metrics);
                indexedMetricIds.UnionWith(
                    metrics.Select(metric => metric.Id));
            }
        }

        Assert.Equal(MetricRegistry.All.Count, indexedMetricIds.Count);
    }

    [Fact]
    public void InstanceKeysRoundTripForEveryMetricScope()
    {
        foreach (MetricDefinition definition in MetricRegistry.All)
        {
            string? deviceId = definition.Scope == MetricScope.PerDevice
                ? "device-id"
                : null;
            string key = MetricInstanceKey.Create(definition, deviceId);

            Assert.True(MetricInstanceKey.TryParse(
                key,
                out string metricId,
                out string? parsedDeviceId));
            Assert.Equal(definition.Id, metricId);
            Assert.Equal(deviceId, parsedDeviceId);
        }
    }

    [Fact]
    public void PerDeviceMetricRequiresDeviceIdentity()
    {
        MetricDefinition definition =
            MetricRegistry.Get(MetricRegistry.GpuUsage);

        Assert.Throws<ArgumentException>(
            () => MetricInstanceKey.Create(definition, null));
    }
}
