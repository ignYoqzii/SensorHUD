using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable editor state for one global or device-specific metric.
/// </summary>
public sealed class MetricSettingsViewModel : ObservableObject
{
    private bool _isVisible;
    private string _template;
    private PrecisionOption _selectedPrecision;

    internal MetricSettingsViewModel(
        string key,
        MetricDefinition definition,
        string? deviceName,
        MetricDisplaySettings? settings)
    {
        Key = key;
        Name = definition.Label;
        DeviceName = deviceName ?? string.Empty;
        UnitDescription = $"Unit: {definition.Unit}";
        _isVisible =
            settings?.IsVisible ?? definition.IsVisibleByDefault;
        _template = string.IsNullOrWhiteSpace(settings?.Template)
            ? definition.DefaultTemplate
            : settings.Template;

        List<PrecisionOption> options =
        [
            new($"Default ({definition.DefaultPrecision})", null),
        ];
        for (int precision = SettingsDefaults.MinimumPrecision;
             precision <= SettingsDefaults.MaximumPrecision;
             precision++)
        {
            options.Add(new PrecisionOption(
                precision == 1
                    ? "1 decimal"
                    : $"{precision} decimals",
                precision));
        }

        PrecisionOptions = options;
        _selectedPrecision = options.First(option =>
            option.Value == settings?.Precision);
    }

    public event EventHandler? Changed;

    public string Key { get; }

    public string Name { get; }

    public string DeviceName { get; }

    public string UnitDescription { get; }

    public IReadOnlyList<PrecisionOption> PrecisionOptions { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetSetting(ref _isVisible, value);
    }

    public string Template
    {
        get => _template;
        set => SetSetting(ref _template, value ?? string.Empty);
    }

    public PrecisionOption SelectedPrecision
    {
        get => _selectedPrecision;
        set
        {
            if (value is not null)
            {
                SetSetting(ref _selectedPrecision, value);
            }
        }
    }

    internal MetricDisplaySettings ToSettings() => new()
    {
        IsVisible = IsVisible,
        Template = Template,
        Precision = SelectedPrecision.Value,
    };

    private void SetSetting<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// User-facing precision choice. Null means the registry default.
/// </summary>
public sealed record PrecisionOption(string Label, int? Value);

/// <summary>
/// One settings card containing related metrics.
/// </summary>
public sealed class MetricGroupViewModel
{
    internal MetricGroupViewModel(
        string name,
        IReadOnlyList<MetricSettingsViewModel> metrics)
    {
        Name = name;
        Metrics = metrics;
        MetricCountText = metrics.Count == 1
            ? "1 metric"
            : $"{metrics.Count} metrics";
    }

    public string Name { get; }

    public string MetricCountText { get; }

    public IReadOnlyList<MetricSettingsViewModel> Metrics { get; }
}
