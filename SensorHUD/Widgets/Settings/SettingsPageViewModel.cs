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
/// 2. New metric or category: declare its metadata in the metric registry and
///    publish readings; settings cards are generated here automatically.
/// 3. New independent source: implement a provider and register it in the
///    collector's TelemetrySampler.
/// </summary>
public sealed class SettingsPageViewModel
{
    private readonly Dictionary<string, MetricDisplaySettings>
        _savedMetricSettings;

    internal SettingsPageViewModel(
        WidgetSettings settings,
        TelemetrySnapshot? snapshot)
    {
        WidgetSettings normalized = SettingsValidator.Normalize(settings);
        _savedMetricSettings = CloneMetricSettings(normalized.Metrics);
        Layout = new LayoutSettingsViewModel(normalized);
        Appearance = new AppearanceSettingsViewModel(normalized.Appearance);
        Status = new CollectorStatusViewModel();
        MetricCategories = CreateMetricCategories(snapshot);

        Layout.Changed += Section_Changed;
        Appearance.Changed += Section_Changed;
    }

    public event EventHandler? Changed;

    public LayoutSettingsViewModel Layout { get; }

    public AppearanceSettingsViewModel Appearance { get; }

    public CollectorStatusViewModel Status { get; }

    public IReadOnlyList<MetricCategoryViewModel> MetricCategories { get; }

#pragma warning disable CA1822 // Mark members as static
    public string VersionText
#pragma warning restore CA1822 // Mark members as static
    {
        get
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"Version {version.Major}.{version.Minor}.{version.Build}";
        }
    }

#pragma warning disable CA1822 // Mark members as static
    public string CopyrightText => $"© {DateTime.Now.Year} yoqzii - All rights reserved.";
#pragma warning restore CA1822 // Mark members as static

    internal WidgetSettings ToSettings()
    {
        WidgetSettings result = SettingsDefaults.Create();
        result.Metrics = CloneMetricSettings(_savedMetricSettings);
        Layout.ApplyTo(result);
        Appearance.ApplyTo(result);

        foreach (MetricSettingsViewModel metric in
                 MetricCategories.SelectMany(category => category.Metrics))
        {
            result.Metrics[metric.Key] = metric.ToSettings();
        }

        return result;
    }

    private List<MetricCategoryViewModel> CreateMetricCategories(
        TelemetrySnapshot? snapshot)
    {
        List<MetricCategoryViewModel> categories =
            new(MetricRegistry.Categories.Count);
        foreach (MetricCategoryDefinition category in
                 MetricRegistry.Categories)
        {
            MetricDefinition[] definitions = [.. MetricRegistry.All
                .Where(definition =>
                    definition.Category == category.Id)
                .OrderBy(definition => definition.SortOrder)];
            MetricDefinition[] globalDefinitions = [.. definitions.Where(definition => !definition.IsPerDevice)];
            if (globalDefinitions.Length > 0)
            {
                categories.Add(new MetricCategoryViewModel(
                    category.Name,
                    category.Description,
                    (List<MetricSettingsViewModel>)[.. globalDefinitions
                        .Select(definition =>
                            CreateMetric(definition, null))]));
            }

            MetricDefinition[] deviceDefinitions = [.. definitions.Where(definition => definition.IsPerDevice)];
            if (deviceDefinitions.Length > 0)
            {
                AddDeviceCategories(
                    categories,
                    category,
                    deviceDefinitions,
                    snapshot);
            }
        }

        return categories;
    }

    /// <summary>
    /// Adds one category card per detected device for per-device metrics.
    /// </summary>
    private void AddDeviceCategories(
        List<MetricCategoryViewModel> categories,
        MetricCategoryDefinition category,
        IReadOnlyList<MetricDefinition> definitions,
        TelemetrySnapshot? snapshot)
    {
        var devices = (snapshot?.Readings ?? [])
            .Where(reading =>
                MetricRegistry.TryGet(
                    reading.MetricId,
                    out MetricDefinition definition) &&
                definition.Category == category.Id &&
                definition.IsPerDevice &&
                !string.IsNullOrWhiteSpace(reading.DeviceId))
            .GroupBy(reading => reading.DeviceId!, StringComparer.Ordinal)
            .OrderBy(
                device => device.First().DeviceName,
                StringComparer.CurrentCultureIgnoreCase);

        foreach (var device in devices)
        {
            string deviceName = string.IsNullOrWhiteSpace(
                device.First().DeviceName)
                ? category.Name
                : device.First().DeviceName!;
            List<MetricSettingsViewModel> metrics = [.. definitions
                .Select(definition =>
                    CreateMetric(definition, device.Key))];
            categories.Add(new MetricCategoryViewModel(
                $"{category.Name} - {deviceName}",
                category.Description,
                metrics));
        }
    }

    private MetricSettingsViewModel CreateMetric(
        MetricDefinition definition,
        string? deviceId)
    {
        string key = MetricInstanceKey.Create(definition, deviceId);
        _savedMetricSettings.TryGetValue(
            key,
            out MetricDisplaySettings? preference);
        MetricSettingsViewModel metric = new(
            key,
            definition,
            preference);
        metric.Changed += Section_Changed;
        return metric;
    }

    private void Section_Changed(object? sender, EventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);

    private static Dictionary<string, MetricDisplaySettings> CloneMetricSettings(
        IReadOnlyDictionary<string, MetricDisplaySettings> preferences) =>
        preferences.ToDictionary(
            pair => pair.Key,
            pair => new MetricDisplaySettings
            {
                IsVisible = pair.Value.IsVisible,
                Format = pair.Value.Format,
                Decimals = pair.Value.Decimals,
            },
            StringComparer.Ordinal);
}
