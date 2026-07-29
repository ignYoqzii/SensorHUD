using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SensorHUD.Core.Metrics;

/// <summary>
/// The single source of truth for supported metrics and their display
/// defaults. To add a metric, add one definition here and publish its reading
/// from a collector provider.
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

    private static readonly IReadOnlyList<MetricDefinition> OrderedDefinitions =
        new ReadOnlyCollection<MetricDefinition>(
        [
            new(Fps, MetricGroup.FrameRate, "FPS", "FPS",
                "{name}: {value} {unit}", 0, true, 0),
            new(OnePercentLow, MetricGroup.FrameRate, "1% Low", "FPS",
                "{name}: {value} {unit}", 0, true, 1),
            new(Frametime, MetricGroup.FrameRate, "Frametime", "ms",
                "{name}: {value} {unit}", 1, true, 2),
            new(CpuUsage, MetricGroup.Cpu, "Usage", "%",
                "{device} Usage: {value}{unit}", 0, true, 0),
            new(CpuTemperature, MetricGroup.Cpu, "Temperature", "°C",
                "{device} Temp: {value}{unit}", 0, true, 1),
            new(GpuUsage, MetricGroup.Gpu, "Usage", "%",
                "{device} Usage: {value}{unit}", 0, true, 0, true),
            new(GpuTemperature, MetricGroup.Gpu, "Temperature", "°C",
                "{device} Temp: {value}{unit}", 0, true, 1, true),
            new(GpuVramUsage, MetricGroup.Gpu, "VRAM usage", "%",
                "{device} VRAM: {value}{unit}", 0, true, 2, true),
            new(GpuVramUsed, MetricGroup.Gpu, "VRAM used", "GB",
                "{device} VRAM Used: {value} {unit}", 1, false, 3, true),
            new(GpuVramTotal, MetricGroup.Gpu, "VRAM total", "GB",
                "{device} VRAM Total: {value} {unit}", 1, false, 4, true),
            new(MemoryUsage, MetricGroup.Memory, "Usage", "%",
                "{device} Usage: {value}{unit}", 0, true, 0),
            new(MemoryUsed, MetricGroup.Memory, "Used", "GB",
                "{device} Used: {value} {unit}", 1, false, 1),
            new(MemoryTotal, MetricGroup.Memory, "Total", "GB",
                "{device} Total: {value} {unit}", 1, false, 2),
            new(NetworkSend, MetricGroup.Network, "Send", "Mbps",
                "↑ {value} {unit}", 1, true, 0),
            new(NetworkReceive, MetricGroup.Network, "Receive", "Mbps",
                "↓ {value} {unit}", 1, true, 1),
        ]);

    private static readonly IReadOnlyDictionary<string, MetricDefinition> ById =
        new ReadOnlyDictionary<string, MetricDefinition>(
            OrderedDefinitions.ToDictionary(
                definition => definition.Id,
                StringComparer.Ordinal));

    /// <summary>
    /// Gets definitions in their stable declaration order.
    /// </summary>
    public static IReadOnlyList<MetricDefinition> All => OrderedDefinitions;

    /// <summary>
    /// Looks up a definition by its base metric ID.
    /// </summary>
    public static bool TryGet(
        string metricId,
        out MetricDefinition definition) =>
        ById.TryGetValue(metricId, out definition!);

    /// <summary>
    /// Gets a definition or throws when provider code uses an unknown ID.
    /// </summary>
    public static MetricDefinition Get(string metricId) =>
        TryGet(metricId, out MetricDefinition definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Metric '{metricId}' is not registered.");

    /// <summary>
    /// Returns the user-facing English label for a settings group.
    /// </summary>
    public static string GetGroupLabel(MetricGroup group) => group switch
    {
        MetricGroup.FrameRate => "Frame rate",
        MetricGroup.Cpu => "CPU",
        MetricGroup.Gpu => "GPU",
        MetricGroup.Memory => "Memory",
        MetricGroup.Network => "Network",
        _ => group.ToString(),
    };

    /// <summary>
    /// Returns the stable display order for a settings group.
    /// </summary>
    public static int GetGroupSortOrder(MetricGroup group) => group switch
    {
        MetricGroup.FrameRate => 0,
        MetricGroup.Cpu => 100,
        MetricGroup.Gpu => 200,
        MetricGroup.Memory => 300,
        MetricGroup.Network => 400,
        _ => int.MaxValue,
    };
}
