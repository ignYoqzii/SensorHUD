using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace SensorHUD.Core.Metrics;

/// <summary>
/// Central source of truth for metric categories and metrics. The settings UI
/// and telemetry presenter are generated from this metadata.
/// </summary>
public static class MetricRegistry
{
    public const string Fps = "fps";
    public const string OnePercentLow = "fps.onePercentLow";
    public const string Frametime = "fps.frametime";
    public const string CpuUsage = "cpu.usage";
    public const string CpuTemperature = "cpu.temperature";
    public const string GpuUsage = "gpu.usage";
    public const string GpuTemperature = "gpu.temperature";
    public const string GpuVramUsage = "gpu.vramUsage";
    public const string GpuVramUsed = "gpu.vramUsed";
    public const string GpuVramTotal = "gpu.vramTotal";
    public const string MemoryUsage = "memory.usage";
    public const string MemoryUsed = "memory.used";
    public const string MemoryTotal = "memory.total";
    public const string NetworkSend = "network.send";
    public const string NetworkReceive = "network.receive";
    public const string Ping = "network.ping";
    public const string PacketLoss = "network.packetLoss";

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
            new()
            {
                Id = CpuUsage,
                Category = MetricCategory.Cpu,
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
                Id = CpuTemperature,
                Category = MetricCategory.Cpu,
                Name = "Temperature",
                Unit = "°C",
                Format = "{device} Temp: {value}{unit}",
                Decimals = 0,
                TextColor = "#FFFFFFFF",
                ValueUnitColor = "#FFFFFFFF",
                SortOrder = 1,
            },
            new()
            {
                Id = GpuUsage,
                Category = MetricCategory.Gpu,
                Name = "Usage",
                Unit = "%",
                Format = "{device} Usage: {value}{unit}",
                Decimals = 0,
                TextColor = "#FFFFFFFF",
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
                Format = "{device} Temp: {value}{unit}",
                Decimals = 0,
                TextColor = "#FFFFFFFF",
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
            new()
            {
                Id = Ping,
                Category = MetricCategory.Network,
                Name = "Ping",
                Unit = "ms",
                Format = "{name}: {value} {unit}",
                Decimals = 0,
                TextColor = "#FFFFFFFF",
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
                SortOrder = 3,
            },
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
