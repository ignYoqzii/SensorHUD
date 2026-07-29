using System;
using System.Collections.Generic;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Telemetry;

/// <summary>
/// Joins readings, registry definitions, user settings, and frontend
/// connection state into an ordered display model.
/// </summary>
internal static class TelemetryPresenter
{
    public static TelemetryDisplayModel Create(
        WidgetSettings settings,
        TelemetrySnapshot? snapshot,
        CollectorConnectionStatus connection)
    {
        Dictionary<string, MetricReading> globalReadings =
            new(StringComparer.Ordinal);
        Dictionary<(string MetricId, string DeviceId), MetricReading>
            deviceReadings = [];
        Dictionary<(MetricCategory Category, string DeviceId), string?>
            devices = [];

        foreach (MetricReading reading in snapshot?.Readings ?? [])
        {
            if (!MetricRegistry.TryGet(
                    reading.MetricId,
                    out MetricDefinition definition))
            {
                continue;
            }

            if (definition.IsPerDevice)
            {
                if (string.IsNullOrWhiteSpace(reading.DeviceId))
                {
                    continue;
                }

                // The latest duplicate wins. A malformed provider can affect
                // one instance without crashing the entire overlay.
                deviceReadings[(reading.MetricId, reading.DeviceId)] =
                    reading;
                devices[(definition.Category, reading.DeviceId)] =
                    reading.DeviceName;
            }
            else
            {
                globalReadings[reading.MetricId] = reading;
            }
        }

        List<PresentedMetric> metrics = new(MetricRegistry.All.Count);
        foreach (MetricDefinition definition in MetricRegistry.All)
        {
            if (definition.IsPerDevice)
            {
                foreach (((MetricCategory category, string deviceId),
                         string? deviceName) in devices)
                {
                    if (category != definition.Category)
                    {
                        continue;
                    }

                    if (!deviceReadings.TryGetValue(
                            (definition.Id, deviceId),
                            out MetricReading? reading))
                    {
                        reading = new MetricReading
                        {
                            MetricId = definition.Id,
                            DeviceId = deviceId,
                            DeviceName = deviceName,
                        };
                    }

                    AddMetric(metrics, definition, reading, settings);
                }
            }
            else
            {
                globalReadings.TryGetValue(
                    definition.Id,
                    out MetricReading? reading);
                AddMetric(metrics, definition, reading, settings);
            }
        }

        metrics.Sort(CompareMetrics);
        return new TelemetryDisplayModel(
            metrics,
            GetStatusText(connection, snapshot));
    }

    private static void AddMetric(
        List<PresentedMetric> target,
        MetricDefinition definition,
        MetricReading? reading,
        WidgetSettings settings)
    {
        string key;
        try
        {
            key = MetricInstanceKey.Create(definition, reading?.DeviceId);
        }
        catch (ArgumentException)
        {
            // A malformed per-device reading is ignored at the frontend
            // boundary instead of destabilizing the entire widget.
            return;
        }

        settings.Metrics.TryGetValue(
            key,
            out MetricDisplaySettings? preference);
        if (!(preference?.IsVisible ?? definition.IsVisibleByDefault))
        {
            return;
        }

        target.Add(new PresentedMetric(
            key,
            definition,
            reading,
            preference,
            MetricFormatter.Format(definition, reading, preference)));
    }

    private static int CompareMetrics(PresentedMetric left, PresentedMetric right)
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
            left.Reading?.DeviceName,
            right.Reading?.DeviceName,
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
/// One metric instance ready for XAML rendering.
/// </summary>
internal sealed record PresentedMetric(
    string Key,
    MetricDefinition Definition,
    MetricReading? Reading,
    MetricDisplaySettings? Settings,
    IReadOnlyList<MetricTextPart> Parts);

/// <summary>
/// Complete frontend display state for one render pass.
/// </summary>
internal sealed record TelemetryDisplayModel(
    IReadOnlyList<PresentedMetric> Metrics,
    string? StatusText);
