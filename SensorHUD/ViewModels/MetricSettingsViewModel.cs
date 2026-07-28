using System;
using System.Collections.Generic;
using System.Linq;
using SensorHUD.Models;
using SensorHUD.Shared;

namespace SensorHUD.ViewModels;

/// <summary>
/// Bindable editor state for one catalog metric.
/// </summary>
public sealed class MetricSettingsViewModel : ObservableObject
{
    private bool _isEnabled;
    private string _format;
    private PrecisionOption? _selectedPrecision;

    internal MetricSettingsViewModel(
        MetricDefinition definition,
        MetricPreference? preference)
    {
        Id = definition.Id;
        Name = definition.Name;
        UnitDescription = $"Unit: {definition.Unit}";
        _isEnabled =
            preference?.IsEnabled ?? definition.EnabledByDefault;
        _format = string.IsNullOrWhiteSpace(preference?.Format)
            ? definition.DefaultFormat
            : preference.Format;

        List<PrecisionOption> precisionOptions =
        [
            new(
                $"Default ({definition.DecimalPlaces})",
                Value: null),
        ];
        for (int decimalPlaces = TelemetryDefaults.MinimumDecimalPlaces;
             decimalPlaces <= TelemetryDefaults.MaximumDecimalPlaces;
             decimalPlaces++)
        {
            precisionOptions.Add(new PrecisionOption(
                decimalPlaces == 1
                    ? "1 decimal"
                    : $"{decimalPlaces} decimals",
                decimalPlaces));
        }

        PrecisionOptions = precisionOptions;
        _selectedPrecision = PrecisionOptions.First(option =>
            option.Value == preference?.DecimalPlaces);
    }

    public event EventHandler? SettingsChanged;

    public string Id { get; }

    public string Name { get; }

    public string UnitDescription { get; }

    public IReadOnlyList<PrecisionOption> PrecisionOptions { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Format
    {
        get => _format;
        set
        {
            value ??= string.Empty;
            if (SetProperty(ref _format, value))
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public PrecisionOption? SelectedPrecision
    {
        get => _selectedPrecision;
        set
        {
            if (value is not null &&
                SetProperty(ref _selectedPrecision, value))
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    internal MetricPreference ToPreference()
    {
        return new MetricPreference
        {
            Id = Id,
            IsEnabled = IsEnabled,
            Format = Format,
            DecimalPlaces = SelectedPrecision?.Value,
        };
    }
}

/// <summary>
/// One user-facing decimal-precision choice. A null value means that the
/// metric catalog decides the precision.
/// </summary>
public sealed record PrecisionOption(string Label, int? Value);

/// <summary>
/// Bindable group displayed as one hardware card in the settings widget.
/// </summary>
public sealed class MetricSectionViewModel
{
    internal MetricSectionViewModel(
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
