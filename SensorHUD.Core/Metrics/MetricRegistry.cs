using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace SensorHUD.Core.Metrics;

/// <summary>
/// Central source of truth for metric categories and metrics. Provider-owned
/// declaration files contribute definitions; settings and presentation
/// consume one flattened, provider-neutral registry.
/// </summary>
public static partial class MetricRegistry
{
    private static readonly MetricCategoryDefinition[] CategoryDefinitions =
        [
            new()
            {
                Id = MetricCategory.FrameRate,
                Name = "Frame Rate",
                Description =
                    "Frame presentation performance for the foreground process.",
                SortOrder = 0,
            },
            new()
            {
                Id = MetricCategory.Cpu,
                Name = "CPU",
                Description = "Processor utilization and temperature.",
                SortOrder = 100,
            },
            new()
            {
                Id = MetricCategory.Gpu,
                Name = "GPU",
                Description =
                    "Graphics utilization and temperature.",
                SortOrder = 200,
            },
            new()
            {
                Id = MetricCategory.Memory,
                Name = "Memory",
                Description = "System memory utilization and capacity.",
                SortOrder = 300,
            },
            new()
            {
                Id = MetricCategory.Network,
                Name = "Network",
                Description =
                    "Adapter throughput and general Internet connection stability.",
                SortOrder = 400,
            },
        ];

    private static readonly IReadOnlyList<MetricCategoryDefinition>
        OrderedCategories = new ReadOnlyCollection<MetricCategoryDefinition>(
            [.. CategoryDefinitions.OrderBy(category => category.SortOrder)]);

    private static readonly IReadOnlyList<MetricDefinition>
        OrderedDefinitions = new ReadOnlyCollection<MetricDefinition>(
        [
            .. CreateFrameRateDefinitions(),
            .. CreateCpuDefinitions(),
            .. CreateGpuDefinitions(),
            .. CreateMemoryDefinitions(),
            .. CreateNetworkAdapterDefinitions(),
            .. CreateInternetPathDefinitions(),
        ]);

    private static readonly ReadOnlyDictionary<MetricCategory,
        MetricCategoryDefinition> CategoriesById =
        new(
            CategoryDefinitions.ToDictionary(category => category.Id));

    private static readonly ReadOnlyDictionary<string, MetricDefinition>
        MetricsById = new(
            OrderedDefinitions.ToDictionary(
                definition => definition.Id,
                StringComparer.Ordinal));

    private static readonly ReadOnlyDictionary<
        (MetricCategory Category, MetricScope Scope),
        IReadOnlyList<MetricDefinition>> MetricsByCategoryAndScope =
        BuildMetricsByCategoryAndScope();

    static MetricRegistry()
    {
        ValidateDefinitions();
    }

    /// <summary>
    /// Gets categories in stable display order.
    /// </summary>
    public static IReadOnlyList<MetricCategoryDefinition> Categories =>
        OrderedCategories;

    /// <summary>
    /// Gets metrics in stable declaration order.
    /// </summary>
    public static IReadOnlyList<MetricDefinition> All => OrderedDefinitions;

    /// <summary>
    /// Gets category presentation metadata.
    /// </summary>
    public static MetricCategoryDefinition GetCategory(
        MetricCategory category) =>
        CategoriesById.TryGetValue(
            category,
            out MetricCategoryDefinition? definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Metric category '{category}' is not registered.");

    /// <summary>
    /// Gets the metrics in one category and scope in stable display order.
    /// </summary>
    public static IReadOnlyList<MetricDefinition> GetMetrics(
        MetricCategory category,
        MetricScope scope) =>
        MetricsByCategoryAndScope.TryGetValue(
            (category, scope),
            out IReadOnlyList<MetricDefinition>? definitions)
            ? definitions
            : [];

    /// <summary>
    /// Looks up a definition by its base metric ID.
    /// </summary>
    public static bool TryGet(
        string metricId,
        out MetricDefinition definition) =>
        MetricsById.TryGetValue(metricId, out definition!);

    /// <summary>
    /// Gets a definition or throws when provider code uses an unknown ID.
    /// </summary>
    public static MetricDefinition Get(string metricId) =>
        TryGet(metricId, out MetricDefinition definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Metric '{metricId}' is not registered.");

    private static ReadOnlyDictionary<
        (MetricCategory Category, MetricScope Scope),
        IReadOnlyList<MetricDefinition>> BuildMetricsByCategoryAndScope()
    {
        Dictionary<
            (MetricCategory Category, MetricScope Scope),
            IReadOnlyList<MetricDefinition>> result = [];
        foreach (IGrouping<
                     (MetricCategory Category, MetricScope Scope),
                     MetricDefinition> group in OrderedDefinitions.GroupBy(
                     definition =>
                         (definition.Category, definition.Scope)))
        {
            result.Add(
                group.Key,
                new ReadOnlyCollection<MetricDefinition>(
                    [.. group.OrderBy(definition =>
                        definition.SortOrder)]));
        }

        return new(result);
    }

    private static void ValidateDefinitions()
    {
        HashSet<int> categorySortOrders = [];
        foreach (MetricCategoryDefinition category in OrderedCategories)
        {
            if (!categorySortOrders.Add(category.SortOrder))
            {
                throw new InvalidOperationException(
                    $"Category sort order '{category.SortOrder}' is duplicated.");
            }
        }

        foreach (MetricDefinition definition in OrderedDefinitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new InvalidOperationException(
                    "Metric IDs cannot be empty.");
            }

            _ = GetCategory(definition.Category);
            if (!Enum.IsDefined(definition.Scope))
            {
                throw new InvalidOperationException(
                    $"Metric '{definition.Id}' has an invalid scope.");
            }

            if (definition.Decimals <
                    MetricDisplayConstraints.MinimumDecimals ||
                definition.Decimals >
                    MetricDisplayConstraints.MaximumDecimals)
            {
                throw new InvalidOperationException(
                    $"Metric '{definition.Id}' has unsupported decimals.");
            }

            if (!IsArgbColor(definition.TextColor) ||
                !IsArgbColor(definition.ValueUnitColor))
            {
                throw new InvalidOperationException(
                    $"Metric '{definition.Id}' has an invalid default color.");
            }
        }

        foreach (IGrouping<MetricCategory, MetricDefinition> category in
                 OrderedDefinitions.GroupBy(definition =>
                     definition.Category))
        {
            HashSet<int> metricSortOrders = [];
            foreach (MetricDefinition definition in category)
            {
                if (!metricSortOrders.Add(definition.SortOrder))
                {
                    throw new InvalidOperationException(
                        $"Metric sort order '{definition.SortOrder}' is " +
                        $"duplicated in category '{category.Key}'.");
                }
            }
        }
    }

    private static bool IsArgbColor(string value) =>
        value.Length == 9 &&
        value[0] == '#' &&
        uint.TryParse(
            value.AsSpan(1),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out _);
}
