using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;
using Windows.ApplicationModel;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Composes the focused settings sections used by the page's compiled
/// bindings.
///
/// Extension path:
/// 1. New global setting: add its model/default/validation, expose it through
///    the relevant section view model, then add one XAML control.
/// 2. New metric: add one registry definition and one provider reading.
/// 3. New hardware source: implement a provider and register it in the
///    collector's TelemetrySampler.
/// </summary>
public sealed class SettingsPageViewModel
{
    private readonly Dictionary<string, MetricDisplaySettings>
        _retainedPreferences;

    internal SettingsPageViewModel(
        WidgetSettings settings,
        TelemetrySnapshot? snapshot)
    {
        WidgetSettings normalized = SettingsValidator.Normalize(settings);
        _retainedPreferences = ClonePreferences(normalized.Metrics);
        Layout = new LayoutSettingsViewModel(normalized);
        Appearance = new AppearanceSettingsViewModel(normalized.Appearance);
        Status = new CollectorStatusViewModel();
        MetricGroups = CreateMetricGroups(snapshot);

        Layout.Changed += Section_Changed;
        Appearance.Changed += Section_Changed;
    }

    public event EventHandler? Changed;

    public LayoutSettingsViewModel Layout { get; }

    public AppearanceSettingsViewModel Appearance { get; }

    public CollectorStatusViewModel Status { get; }

    public IReadOnlyList<MetricGroupViewModel> MetricGroups { get; }

    public string VersionText
    {
        get
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"Version {version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public string CopyrightText => $"© {DateTime.Now.Year} yoqzii - All rights reserved.";

    internal WidgetSettings ToSettings()
    {
        WidgetSettings result = SettingsDefaults.Create();
        result.Metrics = ClonePreferences(_retainedPreferences);
        Layout.ApplyTo(result);
        Appearance.ApplyTo(result);

        foreach (MetricSettingsViewModel metric in
                 MetricGroups.SelectMany(group => group.Metrics))
        {
            result.Metrics[metric.Key] = metric.ToSettings();
        }

        return SettingsValidator.Normalize(result);
    }

    private IReadOnlyList<MetricGroupViewModel> CreateMetricGroups(
        TelemetrySnapshot? snapshot)
    {
        List<MetricGroupViewModel> groups = [];
        foreach (MetricGroup group in Enum.GetValues<MetricGroup>())
        {
            if (group == MetricGroup.Gpu)
            {
                AddGpuGroups(groups, snapshot);
                continue;
            }

            List<MetricSettingsViewModel> metrics = MetricRegistry.All
                .Where(definition => definition.Group == group)
                .OrderBy(definition => definition.SortOrder)
                .Select(definition => CreateMetric(definition, null, null))
                .ToList();
            groups.Add(new MetricGroupViewModel(
                MetricRegistry.GetGroupLabel(group),
                metrics));
        }

        return groups;
    }

    private void AddGpuGroups(
        ICollection<MetricGroupViewModel> groups,
        TelemetrySnapshot? snapshot)
    {
        var devices = (snapshot?.Readings ?? [])
            .Where(reading =>
                MetricRegistry.TryGet(
                    reading.MetricId,
                    out MetricDefinition definition) &&
                definition.Group == MetricGroup.Gpu &&
                !string.IsNullOrWhiteSpace(reading.DeviceId))
            .GroupBy(reading => reading.DeviceId!, StringComparer.Ordinal)
            .OrderBy(
                device => device.First().DeviceName,
                StringComparer.CurrentCultureIgnoreCase);

        foreach (var device in devices)
        {
            string deviceName = string.IsNullOrWhiteSpace(
                device.First().DeviceName)
                ? "GPU"
                : device.First().DeviceName!;
            List<MetricSettingsViewModel> metrics = MetricRegistry.All
                .Where(definition => definition.Group == MetricGroup.Gpu)
                .OrderBy(definition => definition.SortOrder)
                .Select(definition =>
                    CreateMetric(definition, device.Key, deviceName))
                .ToList();
            groups.Add(new MetricGroupViewModel(
                $"GPU - {deviceName}",
                metrics));
        }
    }

    private MetricSettingsViewModel CreateMetric(
        MetricDefinition definition,
        string? deviceId,
        string? deviceName)
    {
        string key = MetricInstanceKey.Create(definition, deviceId);
        _retainedPreferences.TryGetValue(
            key,
            out MetricDisplaySettings? preference);
        MetricSettingsViewModel metric = new(
            key,
            definition,
            deviceName,
            preference);
        metric.Changed += Section_Changed;
        return metric;
    }

    private void Section_Changed(object? sender, EventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);

    private static Dictionary<string, MetricDisplaySettings> ClonePreferences(
        IReadOnlyDictionary<string, MetricDisplaySettings> preferences) =>
        preferences.ToDictionary(
            pair => pair.Key,
            pair => new MetricDisplaySettings
            {
                IsVisible = pair.Value.IsVisible,
                Template = pair.Value.Template,
                Precision = pair.Value.Precision,
            },
            StringComparer.Ordinal);
}
