using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                SortOrder = 0,
                IsPerDevice = true,
            },
            new()
            {
                Id = GpuTemperature,
                Category = MetricCategory.Gpu,
                Name = "Temperature",
                Unit = "°C",
                Format = "{device} Temp: {value}{unit}",
                Decimals = 0,
                SortOrder = 1,
                IsPerDevice = true,
            },
            new()
            {
                Id = GpuVramUsage,
                Category = MetricCategory.Gpu,
                Name = "VRAM Usage",
                Unit = "%",
                Format = "{device} VRAM: {value}{unit}",
                Decimals = 0,
                SortOrder = 2,
                IsPerDevice = true,
            },
            new()
            {
                Id = GpuVramUsed,
                Category = MetricCategory.Gpu,
                Name = "VRAM Used",
                Unit = "GB",
                Format = "{device} VRAM Used: {value} {unit}",
                Decimals = 1,
                IsVisibleByDefault = false,
                SortOrder = 3,
                IsPerDevice = true,
            },
            new()
            {
                Id = GpuVramTotal,
                Category = MetricCategory.Gpu,
                Name = "VRAM Total",
                Unit = "GB",
                Format = "{device} VRAM Total: {value} {unit}",
                Decimals = 1,
                IsVisibleByDefault = false,
                SortOrder = 4,
                IsPerDevice = true,
            },
            new()
            {
                Id = MemoryUsage,
                Category = MetricCategory.Memory,
                Name = "Usage",
                Unit = "%",
                Format = "{device} Usage: {value}{unit}",
                Decimals = 0,
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
}
