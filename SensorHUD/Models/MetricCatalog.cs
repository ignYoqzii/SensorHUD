using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Shared;

namespace SensorHUD.Models;

/// <summary>
/// The single catalog for metric labels, units, formats, grouping, and order.
/// Add or adjust a metric here; the display and settings UI adapt themselves.
/// GPU and Hardware definitions are generated dynamically to support real device names.
/// </summary>
internal static class MetricCatalog
{
    private static readonly MetricSection FrameRateSection = new("frame-rate", "Frame rate", 0);
    private static readonly MetricSection CpuSection = new("cpu", "CPU", 100);
    private static readonly MetricSection MemorySection = new("memory", "Memory", 300);
    private static readonly MetricSection NetworkSection = new("network", "Network", 400);

    public static IReadOnlyList<MetricDefinition> CreateForSnapshot(
        TelemetrySnapshot? snapshot)
    {
        List<MetricDefinition> result = [];

        // 1. Extract dynamic names from the snapshot if available
        string cpuDeviceName = snapshot?.Values
            .FirstOrDefault(v => v.Category == MetricCategories.Cpu && !string.IsNullOrWhiteSpace(v.DeviceName))?.DeviceName ?? "CPU";

        string ramDeviceName = snapshot?.Values
            .FirstOrDefault(v => v.Category == MetricCategories.Ram && !string.IsNullOrWhiteSpace(v.DeviceName))?.DeviceName ?? "Memory";

        // 2. Add CPU Metrics with dynamic device names
        result.Add(CreateMetric(MetricIds.CpuUsage, "Usage", CpuSection, 0, "{device} Usage: {value}{unit}", 0, MetricUnits.Percent, cpuDeviceName));
        result.Add(CreateMetric(MetricIds.CpuTemperature, "Temperature", CpuSection, 1, "{device} Temp: {value}{unit}", 0, MetricUnits.Celsius, cpuDeviceName));

        // 3. Add RAM Metrics with dynamic device names
        result.Add(CreateMetric(MetricIds.RamUsage, "Usage", MemorySection, 0, "{device} Usage: {value}{unit}", 0, MetricUnits.Percent, ramDeviceName));

        // 4. Add Static/Global Metrics (FPS, Network)
        result.Add(CreateMetric(MetricIds.Fps, "FPS", FrameRateSection, 0, "FPS: {value} {unit}", 0, MetricUnits.FramesPerSecond, "FPS"));
        result.Add(CreateMetric(MetricIds.OnePercentLow, "1% low", FrameRateSection, 1, "1% low: {value} {unit}", 0, MetricUnits.FramesPerSecond, "1% low"));
        result.Add(CreateMetric(MetricIds.Frametime, "Frametime", FrameRateSection, 2, "Frametime: {value} {unit}", 1, MetricUnits.Milliseconds, "Frametime"));
        result.Add(CreateMetric(MetricIds.NetworkSend, "Send", NetworkSection, 0, "↑ {value} {unit}", 1, MetricUnits.MegabitsPerSecond, "Network"));
        result.Add(CreateMetric(MetricIds.NetworkReceive, "Receive", NetworkSection, 1, "↓ {value} {unit}", 1, MetricUnits.MegabitsPerSecond, "Network"));

        // 5. Process GPUs dynamically
        IEnumerable<IGrouping<string, TelemetryValue>> gpuDevices = snapshot is null
            ? []
            : snapshot.Values
                .Where(value => value.Category == MetricCategories.Gpu)
                .GroupBy(value =>
                {
                    // Extract the unique device identifier between 'gpu.' and the suffix (.usage, .temperature, etc.)
                    string id = value.Id;
                    if (id.StartsWith(MetricIds.GpuPrefix, StringComparison.Ordinal))
                    {
                        int suffixIndex = id.LastIndexOf('.');
                        if (suffixIndex > MetricIds.GpuPrefix.Length)
                        {
                            return id[..suffixIndex]; // e.g., "gpu.abc1234567"
                        }
                    }
                    return id;
                })
                .OrderBy(
                    group => group.First().DeviceName,
                    StringComparer.CurrentCultureIgnoreCase);

        int gpuIndex = 0;
        foreach (IGrouping<string, TelemetryValue> deviceGroup in gpuDevices)
        {
            string deviceName = string.IsNullOrWhiteSpace(deviceGroup.First().DeviceName)
                ? $"GPU {gpuIndex + 1}"
                : deviceGroup.First().DeviceName;

            MetricSection section = new(
                deviceGroup.Key,
                $"GPU - {deviceName}", // Displays clean section title like "GPU - NVIDIA GeForce RTX 4080"
                200 + gpuIndex);

            foreach (TelemetryValue value in deviceGroup.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                // Determine the correct suffix label/format based on MetricIds definitions
                string formatString;
                string metricName = value.Name;

                if (value.Id.EndsWith(MetricIds.VramSuffix, StringComparison.Ordinal))
                {
                    formatString = "{device} VRAM: {value}{unit}";
                }
                else if (value.Id.EndsWith(MetricIds.TemperatureSuffix, StringComparison.Ordinal))
                {
                    formatString = "{device} Temp: {value}{unit}";
                }
                else if (value.Id.EndsWith(MetricIds.UsageSuffix, StringComparison.Ordinal))
                {
                    formatString = "{device} Usage: {value}{unit}";
                }
                else
                {
                    formatString = "{device}: {value}{unit}";
                }

                result.Add(new MetricDefinition(
                    value.Id,
                    metricName,
                    section,
                    value.Unit,
                    formatString,
                    DecimalPlaces: 0,
                    Order: MetricOrder(value.Id),
                    DeviceName: deviceName));
            }

            gpuIndex++;
        }

        return result
            .OrderBy(definition => definition.Section.Order)
            .ThenBy(definition => definition.Order)
            .ToArray();
    }

    private static MetricDefinition CreateMetric(
        string id,
        string name,
        MetricSection section,
        int order,
        string format,
        int decimals,
        string unit,
        string deviceName)
    {
        return new MetricDefinition(
            id,
            name,
            section,
            unit,
            format,
            decimals,
            order,
            DeviceName: deviceName);
    }

    private static int MetricOrder(string metricId)
    {
        if (metricId.EndsWith(MetricIds.UsageSuffix, StringComparison.Ordinal))
        {
            return 0;
        }

        return metricId.EndsWith(MetricIds.TemperatureSuffix, StringComparison.Ordinal) ? 1 : 2;
    }
}
