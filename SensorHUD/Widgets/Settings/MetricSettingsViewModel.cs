using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable editor state for one global or device-specific metric.
/// </summary>
public sealed partial class MetricSettingsViewModel : ObservableObject
{
    private bool _isVisible;
    private string _format;
    private DecimalOption _selectedDecimals;

    internal MetricSettingsViewModel(
        string key,
        MetricDefinition definition,
        MetricDisplaySettings? settings)
    {
        Key = key;
        Name = definition.Name;
        UnitDescription = $"Unit: {definition.Unit}";
        _isVisible =
            settings?.IsVisible ?? definition.IsVisibleByDefault;
        _format = string.IsNullOrWhiteSpace(settings?.Format)
            ? definition.Format
            : settings.Format;

        List<DecimalOption> options =
        [
            new($"Default ({definition.Decimals})", null),
        ];
        for (int decimals = SettingsDefaults.MinimumDecimals;
             decimals <= SettingsDefaults.MaximumDecimals;
             decimals++)
        {
            options.Add(new DecimalOption(
                decimals == 1
                    ? "1 decimal"
                    : $"{decimals} decimals",
                decimals));
        }

        DecimalOptions = options;
        _selectedDecimals = options.First(option =>
            option.Value == settings?.Decimals);
    }

    public event EventHandler? Changed;

    public string Key { get; }

    public string Name { get; }

    public string UnitDescription { get; }

    public IReadOnlyList<DecimalOption> DecimalOptions { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetSetting(ref _isVisible, value);
    }

    public string Format
    {
        get => _format;
        set => SetSetting(ref _format, value ?? string.Empty);
    }

    public DecimalOption SelectedDecimals
    {
        get => _selectedDecimals;
        set
        {
            if (value is not null)
            {
                SetSetting(ref _selectedDecimals, value);
            }
        }
    }

    internal MetricDisplaySettings ToSettings() => new()
    {
        IsVisible = IsVisible,
        Format = Format,
        Decimals = SelectedDecimals.Value,
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
/// User-facing decimal choice. Null means the registry default.
/// </summary>
public sealed record DecimalOption(string Label, int? Value);
