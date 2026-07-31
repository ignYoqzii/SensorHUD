using System;
using System.Collections.Generic;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Telemetry;

/// <summary>
/// Builds stable presentation slots from registry definitions, settings, and
/// declared devices, then joins currently available readings into those
/// slots. Reading absence changes only the displayed value.
/// </summary>
internal static class TelemetryPresenter
{
    public static TelemetryDisplayModel Create(
        WidgetSettings settings,
        TelemetrySnapshot? snapshot,
        CollectorConnectionStatus connection)
    {
        Dictionary<string, MetricReading> readings =
            IndexReadings(snapshot);
        Dictionary<string, MetricInstance> instances =
            IndexInstances(snapshot);
        List<PresentedMetric> metrics = new(
            MetricRegistry.All.Count + instances.Count);

        foreach (MetricDefinition definition in MetricRegistry.All)
        {
            if (definition.Scope != MetricScope.Global)
            {
                continue;
            }

            string key = definition.Id;
            readings.TryGetValue(key, out MetricReading? reading);
            AddSlot(
                metrics,
                settings,
                key,
                definition,
                reading?.DeviceName,
                reading?.Value);
        }

        foreach ((string key, MetricInstance instance) in instances)
        {
            MetricDefinition definition =
                MetricRegistry.Get(instance.MetricId);
            readings.TryGetValue(key, out MetricReading? reading);
            string? deviceName =
                string.IsNullOrWhiteSpace(instance.DeviceName)
                    ? reading?.DeviceName
                    : instance.DeviceName;
            AddSlot(
                metrics,
                settings,
                key,
                definition,
                deviceName,
                reading?.Value);
        }

        metrics.Sort(CompareMetrics);
        return new TelemetryDisplayModel(
            metrics,
            GetStatusText(connection, snapshot));
    }

    private static Dictionary<string, MetricReading> IndexReadings(
        TelemetrySnapshot? snapshot)
    {
        Dictionary<string, MetricReading> result =
            new(StringComparer.Ordinal);
        foreach (MetricReading reading in snapshot?.Readings ?? [])
        {
            if (!MetricRegistry.TryGet(
                    reading.MetricId,
                    out MetricDefinition definition) ||
                !double.IsFinite(reading.Value))
            {
                continue;
            }

            string key;
            if (definition.Scope == MetricScope.PerDevice)
            {
                if (string.IsNullOrWhiteSpace(reading.DeviceId))
                {
                    continue;
                }

                key = MetricInstanceKey.Create(
                    definition,
                    reading.DeviceId);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(reading.DeviceId))
                {
                    continue;
                }

                key = definition.Id;
            }

            // The latest malformed duplicate wins without affecting any
            // other presentation slot.
            result[key] = reading;
        }

        return result;
    }

    private static Dictionary<string, MetricInstance> IndexInstances(
        TelemetrySnapshot? snapshot)
    {
        Dictionary<string, MetricInstance> result =
            new(StringComparer.Ordinal);
        foreach (MetricInstance instance in snapshot?.Instances ?? [])
        {
            if (!MetricRegistry.TryGet(
                    instance.MetricId,
                    out MetricDefinition definition) ||
                definition.Scope != MetricScope.PerDevice ||
                string.IsNullOrWhiteSpace(instance.DeviceId))
            {
                continue;
            }

            string key = MetricInstanceKey.Create(
                definition,
                instance.DeviceId);
            result[key] = instance;
        }

        return result;
    }

    private static void AddSlot(
        ICollection<PresentedMetric> target,
        WidgetSettings settings,
        string key,
        MetricDefinition definition,
        string? deviceName,
        double? value)
    {
        settings.MetricOverrides.TryGetValue(
            key,
            out MetricOverrides? overrides);
        if (!(overrides?.IsVisible ?? definition.IsVisibleByDefault))
        {
            return;
        }

        target.Add(new PresentedMetric(
            key,
            definition,
            deviceName,
            value,
            overrides,
            MetricFormatter.Format(
                definition,
                value,
                deviceName,
                overrides)));
    }

    private static int CompareMetrics(
        PresentedMetric left,
        PresentedMetric right)
    {
        int category = MetricRegistry.GetCategory(
                left.Definition.Category).SortOrder
            .CompareTo(
                MetricRegistry.GetCategory(
                    right.Definition.Category).SortOrder);
        if (category != 0)
        {
            return category;
        }

        int device = string.Compare(
            left.DeviceName,
            right.DeviceName,
            StringComparison.CurrentCultureIgnoreCase);
        return device != 0
            ? device
            : left.Definition.SortOrder.CompareTo(
                right.Definition.SortOrder);
    }

    private static string? GetStatusText(
        CollectorConnectionStatus connection,
        TelemetrySnapshot? snapshot) => connection.State switch
        {
            CollectorConnectionState.Connected when snapshot is null =>
                "Waiting for telemetry",
            CollectorConnectionState.Connected => null,
            CollectorConnectionState.Connecting => "Connecting to collector",
            CollectorConnectionState.Unavailable =>
                "No telemetry data to display",
            _ => "Collector is stopped",
        };
}

/// <summary>
/// One stable registry or declared-device slot ready for XAML rendering.
/// </summary>
internal readonly record struct PresentedMetric(
    string Key,
    MetricDefinition Definition,
    string? DeviceName,
    double? Value,
    MetricOverrides? Overrides,
    IReadOnlyList<MetricTextPart> Parts);

/// <summary>
/// Complete frontend display state for one render pass.
/// </summary>
internal readonly record struct TelemetryDisplayModel(
    IReadOnlyList<PresentedMetric> Metrics,
    string? StatusText);
