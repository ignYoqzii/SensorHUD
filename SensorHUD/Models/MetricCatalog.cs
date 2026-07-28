using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Shared;

namespace SensorHUD.Models;

/// <summary>
/// Single source of truth for metric labels, units, default templates,
/// visibility, grouping, and display order. UI pages consume this catalog and
/// therefore need no hardware-specific control definitions.
/// </summary>
internal static class MetricCatalog
{
    private const int FrameRateSectionOrder = 0;
    private const int CpuSectionOrder = 100;
    private const int FirstGpuSectionOrder = 200;
    private const int MemorySectionOrder = 300;
    private const int NetworkSectionOrder = 400;

    private static readonly MetricSection FrameRateSection =
        new("frame-rate", "Frame rate", FrameRateSectionOrder);
    private static readonly MetricSection CpuSection =
        new("cpu", "CPU", CpuSectionOrder);
    private static readonly MetricSection MemorySection =
        new("memory", "Memory", MemorySectionOrder);
    private static readonly MetricSection NetworkSection =
        new("network", "Network", NetworkSectionOrder);

    /// <summary>
    /// Creates the complete catalog for one snapshot. GPU sections are dynamic
    /// because machines can expose any number of graphics adapters.
    /// </summary>
    public static IReadOnlyList<MetricDefinition> CreateForSnapshot(
        TelemetrySnapshot? snapshot)
    {
        IReadOnlyList<TelemetryValue> readings = snapshot?.Values ?? [];
        string cpuName =
            FindDeviceName(readings, MetricCategories.Cpu, "CPU");
        string memoryName =
            FindDeviceName(readings, MetricCategories.Ram, "Memory");

        List<MetricDefinition> definitions =
        [
            CreateMetric(
                MetricIds.CpuUsage,
                "Usage",
                CpuSection,
                order: 0,
                format: "{device} Usage: {value}{unit}",
                decimals: 0,
                unit: MetricUnits.Percent,
                deviceName: cpuName),
            CreateMetric(
                MetricIds.CpuTemperature,
                "Temperature",
                CpuSection,
                order: 1,
                format: "{device} Temp: {value}{unit}",
                decimals: 0,
                unit: MetricUnits.Celsius,
                deviceName: cpuName),
            CreateMetric(
                MetricIds.RamUsage,
                "Usage",
                MemorySection,
                order: 0,
                format: "{device} Usage: {value}{unit}",
                decimals: 0,
                unit: MetricUnits.Percent,
                deviceName: memoryName),
            CreateMetric(
                MetricIds.RamUsed,
                "Used",
                MemorySection,
                order: 1,
                format: "{device} Used: {value} {unit}",
                decimals: 1,
                unit: MetricUnits.Gigabytes,
                deviceName: memoryName,
                enabledByDefault: false),
            CreateMetric(
                MetricIds.RamTotal,
                "Total",
                MemorySection,
                order: 2,
                format: "{device} Total: {value} {unit}",
                decimals: 1,
                unit: MetricUnits.Gigabytes,
                deviceName: memoryName,
                enabledByDefault: false),
            CreateMetric(
                MetricIds.Fps,
                "FPS",
                FrameRateSection,
                order: 0,
                format: "FPS: {value} {unit}",
                decimals: 0,
                unit: MetricUnits.FramesPerSecond,
                deviceName: "FPS"),
            CreateMetric(
                MetricIds.OnePercentLow,
                "1% Low",
                FrameRateSection,
                order: 1,
                format: "1% Low: {value} {unit}",
                decimals: 0,
                unit: MetricUnits.FramesPerSecond,
                deviceName: "1% Low"),
            CreateMetric(
                MetricIds.Frametime,
                "Frametime",
                FrameRateSection,
                order: 2,
                format: "Frametime: {value} {unit}",
                decimals: 1,
                unit: MetricUnits.Milliseconds,
                deviceName: "Frametime"),
            CreateMetric(
                MetricIds.NetworkSend,
                "Send",
                NetworkSection,
                order: 0,
                format: "↑ {value} {unit}",
                decimals: 1,
                unit: MetricUnits.MegabitsPerSecond,
                deviceName: "Network"),
            CreateMetric(
                MetricIds.NetworkReceive,
                "Receive",
                NetworkSection,
                order: 1,
                format: "↓ {value} {unit}",
                decimals: 1,
                unit: MetricUnits.MegabitsPerSecond,
                deviceName: "Network"),
        ];

        AddGpuDefinitions(definitions, readings);

        return definitions
            .OrderBy(definition => definition.Section.Order)
            .ThenBy(definition => definition.Order)
            .ToArray();
    }

    private static void AddGpuDefinitions(
        List<MetricDefinition> definitions,
        IReadOnlyList<TelemetryValue> readings)
    {
        IEnumerable<IGrouping<string, TelemetryValue>> devices = readings
            .Where(value => value.Category == MetricCategories.Gpu)
            .GroupBy(GetGpuDeviceKey)
            .OrderBy(
                group => group.First().DeviceName,
                StringComparer.CurrentCultureIgnoreCase);

        int gpuIndex = 0;
        foreach (IGrouping<string, TelemetryValue> device in devices)
        {
            string deviceName =
                string.IsNullOrWhiteSpace(device.First().DeviceName)
                    ? $"GPU {gpuIndex + 1}"
                    : device.First().DeviceName;
            MetricSection section = new(
                device.Key,
                $"GPU - {deviceName}",
                FirstGpuSectionOrder + gpuIndex);

            definitions.AddRange(device
                .OrderBy(value => GetGpuMetricOrder(value.Id))
                .ThenBy(value => value.Id, StringComparer.Ordinal)
                .Select(value =>
                    CreateGpuDefinition(value, section, deviceName)));
            gpuIndex++;
        }
    }

    private static MetricDefinition CreateGpuDefinition(
        TelemetryValue reading,
        MetricSection section,
        string deviceName)
    {
        bool isAbsoluteMemory =
            reading.Id.EndsWith(
                MetricIds.VramUsedSuffix,
                StringComparison.Ordinal) ||
            reading.Id.EndsWith(
                MetricIds.VramTotalSuffix,
                StringComparison.Ordinal);

        return new MetricDefinition(
            reading.Id,
            reading.Name,
            section,
            reading.Unit,
            GetGpuDefaultFormat(reading.Id),
            DecimalPlaces:
                reading.Unit == MetricUnits.Gigabytes ? 1 : 0,
            Order: GetGpuMetricOrder(reading.Id),
            EnabledByDefault: !isAbsoluteMemory,
            DeviceName: deviceName);
    }

    private static string GetGpuDefaultFormat(string metricId)
    {
        if (metricId.EndsWith(
            MetricIds.VramUsedSuffix,
            StringComparison.Ordinal))
        {
            return "{device} VRAM Used: {value} {unit}";
        }

        if (metricId.EndsWith(
            MetricIds.VramTotalSuffix,
            StringComparison.Ordinal))
        {
            return "{device} VRAM Total: {value} {unit}";
        }

        if (metricId.EndsWith(
            MetricIds.VramSuffix,
            StringComparison.Ordinal))
        {
            return "{device} VRAM: {value}{unit}";
        }

        if (metricId.EndsWith(
            MetricIds.TemperatureSuffix,
            StringComparison.Ordinal))
        {
            return "{device} Temp: {value}{unit}";
        }

        return metricId.EndsWith(
            MetricIds.UsageSuffix,
            StringComparison.Ordinal)
                ? "{device} Usage: {value}{unit}"
                : "{device}: {value}{unit}";
    }

    private static int GetGpuMetricOrder(string metricId)
    {
        if (metricId.EndsWith(
            MetricIds.UsageSuffix,
            StringComparison.Ordinal))
        {
            return 0;
        }

        if (metricId.EndsWith(
            MetricIds.TemperatureSuffix,
            StringComparison.Ordinal))
        {
            return 1;
        }

        if (metricId.EndsWith(
            MetricIds.VramSuffix,
            StringComparison.Ordinal))
        {
            return 2;
        }

        return metricId.EndsWith(
            MetricIds.VramUsedSuffix,
            StringComparison.Ordinal)
                ? 3
                : 4;
    }

    private static string GetGpuDeviceKey(TelemetryValue reading)
    {
        int suffixIndex = reading.Id.LastIndexOf('.');
        return reading.Id.StartsWith(
                MetricIds.GpuPrefix,
                StringComparison.Ordinal) &&
            suffixIndex > MetricIds.GpuPrefix.Length
                ? reading.Id[..suffixIndex]
                : reading.Id;
    }

    private static string FindDeviceName(
        IReadOnlyList<TelemetryValue> readings,
        string category,
        string fallback)
    {
        return readings
            .FirstOrDefault(value =>
                value.Category == category &&
                !string.IsNullOrWhiteSpace(value.DeviceName))
            ?.DeviceName ?? fallback;
    }

    private static MetricDefinition CreateMetric(
        string id,
        string name,
        MetricSection section,
        int order,
        string format,
        int decimals,
        string unit,
        string deviceName,
        bool enabledByDefault = true)
    {
        return new MetricDefinition(
            id,
            name,
            section,
            unit,
            format,
            decimals,
            order,
            enabledByDefault,
            DeviceName: deviceName);
    }
}
