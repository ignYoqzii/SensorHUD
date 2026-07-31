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
/// 1. New widget setting: add its model/default/validation, expose it through
///    the relevant section view model, then add one XAML control.
/// 2. New metric or category: declare its metadata in the metric registry and
///    publish readings; settings cards are generated here automatically.
/// 3. New independent source: implement a provider and register it in the
///    collector's TelemetrySampler.
/// </summary>
public sealed class SettingsPageViewModel
{
    private readonly Dictionary<string, MetricOverrides>
        _savedMetricOverrides;

    internal SettingsPageViewModel(
        WidgetSettings settings,
        TelemetrySnapshot? snapshot)
    {
        WidgetSettings normalized = SettingsValidator.Normalize(settings);
        _savedMetricOverrides = CloneMetricOverrides(
            normalized.MetricOverrides);
        Layout = new LayoutSettingsViewModel(normalized);
        Appearance = new AppearanceSettingsViewModel(normalized.Appearance);
        Status = new CollectorStatusViewModel();
        MetricCategories = CreateMetricCategories(snapshot);
        PackageVersion version = Package.Current.Id.Version;
        VersionText =
            $"Version {version.Major}.{version.Minor}.{version.Build}";
        CopyrightText =
            $"© {DateTime.Now.Year} yoqzii - All rights reserved.";

        Layout.Changed += Section_Changed;
        Appearance.Changed += Section_Changed;
    }

    public event EventHandler? Changed;

    public LayoutSettingsViewModel Layout { get; }

    public AppearanceSettingsViewModel Appearance { get; }

    public CollectorStatusViewModel Status { get; }

    public IReadOnlyList<MetricCategoryViewModel> MetricCategories { get; }

    public string VersionText { get; }

    public string CopyrightText { get; }

    internal WidgetSettings ToSettings()
    {
        WidgetSettings result = SettingsDefaults.Create();
        CopyMetricOverrides(
            _savedMetricOverrides,
            result.MetricOverrides);
        Layout.ApplyTo(result);
        Appearance.ApplyTo(result);

        foreach (MetricSettingsViewModel metric in
                 MetricCategories.SelectMany(category => category.Metrics))
        {
            result.MetricOverrides.Remove(metric.Key);
            MetricOverrides overrides = metric.ToOverrides();
            if (!overrides.IsEmpty)
            {
                result.MetricOverrides.Add(metric.Key, overrides);
            }
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
            IReadOnlyList<MetricDefinition> globalDefinitions =
                MetricRegistry.GetMetrics(
                    category.Id,
                    MetricScope.Global);
            if (globalDefinitions.Count > 0)
            {
                categories.Add(new MetricCategoryViewModel(
                    category.Name,
                    category.Description,
                    (List<MetricSettingsViewModel>)[.. globalDefinitions
                        .Select(definition =>
                            CreateMetric(definition, null))]));
            }

            IReadOnlyList<MetricDefinition> deviceDefinitions =
                MetricRegistry.GetMetrics(
                    category.Id,
                    MetricScope.PerDevice);
            if (deviceDefinitions.Count > 0)
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
        var devices = (snapshot?.Instances ?? [])
            .Where(instance =>
                MetricRegistry.TryGet(
                    instance.MetricId,
                    out MetricDefinition definition) &&
                definition.Category == category.Id &&
                definition.Scope == MetricScope.PerDevice &&
                !string.IsNullOrWhiteSpace(instance.DeviceId))
            .GroupBy(instance => instance.DeviceId, StringComparer.Ordinal)
            .OrderBy(
                device => device
                    .Select(instance => instance.DeviceName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.CurrentCultureIgnoreCase);

        foreach (var device in devices)
        {
            string? declaredName = device
                .Select(instance => instance.DeviceName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            string deviceName = string.IsNullOrWhiteSpace(declaredName)
                ? category.Name
                : declaredName!;
            HashSet<string> declaredMetrics = new(
                device.Select(instance => instance.MetricId),
                StringComparer.Ordinal);
            List<MetricSettingsViewModel> metrics = [.. definitions
                .Where(definition =>
                    declaredMetrics.Contains(definition.Id))
                .Select(definition =>
                    CreateMetric(definition, device.Key))];
            if (metrics.Count == 0)
            {
                continue;
            }

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
        _savedMetricOverrides.TryGetValue(
            key,
            out MetricOverrides? overrides);
        MetricSettingsViewModel metric = new(
            key,
            definition,
            overrides);
        metric.Changed += Section_Changed;
        return metric;
    }

    private void Section_Changed(object? sender, EventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);

    private static Dictionary<string, MetricOverrides> CloneMetricOverrides(
        Dictionary<string, MetricOverrides> overrides)
    {
        Dictionary<string, MetricOverrides> result =
            new(overrides.Count, StringComparer.Ordinal);
        CopyMetricOverrides(overrides, result);
        return result;
    }

    private static void CopyMetricOverrides(
        IReadOnlyDictionary<string, MetricOverrides> source,
        Dictionary<string, MetricOverrides> destination)
    {
        foreach ((string key, MetricOverrides value) in source)
        {
            destination.Add(
                key,
                new MetricOverrides
                {
                    IsVisible = value.IsVisible,
                    Format = value.Format,
                    Decimals = value.Decimals,
                    TextColor = value.TextColor,
                    ValueUnitColor = value.ValueUnitColor,
                });
        }
    }
}
