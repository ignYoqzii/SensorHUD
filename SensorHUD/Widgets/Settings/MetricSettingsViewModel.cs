using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Presentation;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable editor state for one global or device-specific metric.
/// </summary>
public sealed class MetricSettingsViewModel : EditableSettingsViewModel
{
    private readonly MetricDefinition _definition;
    private bool _isVisible;
    private string _format;
    private DecimalOption _selectedDecimals;
    private readonly MetricColorSettingViewModel _textColor;
    private readonly MetricColorSettingViewModel _valueUnitColor;

    internal MetricSettingsViewModel(
        string key,
        MetricDefinition definition,
        MetricOverrides? overrides)
    {
        _definition = definition;
        Key = key;
        Name = definition.Name;
        UnitDescription = $"Unit: {definition.Unit}";
        _isVisible =
            overrides?.IsVisible ?? definition.IsVisibleByDefault;
        _format = string.IsNullOrWhiteSpace(overrides?.Format)
            ? definition.Format
            : overrides.Format;
        _textColor = CreateColorSetting(
            "Text color",
            "Literal text, {device}, and {name}",
            "Choose metric text color",
            overrides?.TextColor,
            definition.TextColor);
        _valueUnitColor = CreateColorSetting(
            "Value and unit color",
            "{value} and {unit}",
            "Choose metric value and unit color",
            overrides?.ValueUnitColor,
            definition.ValueUnitColor);
        ColorSettings = [_textColor, _valueUnitColor];

        List<DecimalOption> options =
        [
            new($"Default ({definition.Decimals})", null),
        ];
        for (int decimals = MetricDisplayConstraints.MinimumDecimals;
             decimals <= MetricDisplayConstraints.MaximumDecimals;
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
            option.Value == overrides?.Decimals);
    }

    public string Key { get; }

    public string Name { get; }

    public string UnitDescription { get; }

    public IReadOnlyList<DecimalOption> DecimalOptions { get; }

    public MetricColorSettingViewModel[] ColorSettings { get; }

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

    internal MetricOverrides ToOverrides() => new()
    {
        IsVisible = IsVisible == _definition.IsVisibleByDefault
            ? null
            : IsVisible,
        Format = string.Equals(
            Format,
            _definition.Format,
            StringComparison.Ordinal)
                ? null
                : Format,
        Decimals = SelectedDecimals.Value == _definition.Decimals
            ? null
            : SelectedDecimals.Value,
        TextColor = GetColorOverride(
            _textColor.ColorText,
            _definition.TextColor),
        ValueUnitColor = GetColorOverride(
            _valueUnitColor.ColorText,
            _definition.ValueUnitColor),
    };

    private MetricColorSettingViewModel CreateColorSetting(
        string label,
        string description,
        string automationName,
        string? overrideValue,
        string defaultColor)
    {
        MetricColorSettingViewModel setting = new(
            label,
            description,
            automationName,
            XamlTextStyle.ParseColor(
                string.IsNullOrWhiteSpace(overrideValue)
                    ? defaultColor
                    : overrideValue));
        setting.Changed += ColorSetting_Changed;
        return setting;
    }

    private void ColorSetting_Changed(object? sender, EventArgs e) =>
        RaiseChanged();

    private static string? GetColorOverride(
        string value,
        string fallback) =>
        string.Equals(
            value,
            fallback,
            StringComparison.OrdinalIgnoreCase)
                ? null
                : value;
}

/// <summary>
/// User-facing decimal choice. Null means the registry default.
/// </summary>
public sealed record DecimalOption(string Label, int? Value);
