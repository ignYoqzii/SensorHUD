using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SensorHUD.Models;
using SensorHUD.Shared;

namespace SensorHUD.ViewModels;

/// <summary>
/// Bindable state for the editable portion of the settings widget. It maps
/// between UI-friendly values and the small persisted settings contract.
/// </summary>
public sealed class SettingsPageViewModel : ObservableObject
{
    private const double PercentageScale = 100;

    private static readonly string[] AvailableLayouts =
    [
        LayoutNames.Vertical,
        LayoutNames.Horizontal,
    ];

    private static readonly string[] AvailableFontWeights =
    [
        FontWeightNames.Light,
        FontWeightNames.Normal,
        FontWeightNames.SemiBold,
        FontWeightNames.Bold,
        FontWeightNames.Black,
    ];

    private readonly List<MetricPreference> _hiddenPreferences;

    private string _layout;
    private string _horizontalSeparator;
    private double _backgroundOpacityPercent;
    private string _fontFamily;
    private string _fontWeight;
    private double _fontSize;
    private string _fontColor;

    internal SettingsPageViewModel(
        TelemetrySettings settings,
        IReadOnlyList<MetricDefinition> definitions)
    {
        _layout = settings.Layout;
        _horizontalSeparator = settings.HorizontalSeparator;
        _backgroundOpacityPercent =
            settings.BackgroundOpacity * PercentageScale;
        _fontFamily = settings.FontFamily;
        _fontWeight = settings.FontWeight;
        _fontSize = settings.FontSize;
        _fontColor = settings.FontColor;

        HashSet<string> visibleIds = definitions
            .Select(definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        _hiddenPreferences = settings.Metrics
            .Where(preference => !visibleIds.Contains(preference.Id))
            .Select(ClonePreference)
            .ToList();

        List<MetricSectionViewModel> sections = [];
        foreach (IGrouping<string, MetricDefinition> group in
            definitions.GroupBy(definition => definition.Section.Id))
        {
            List<MetricSettingsViewModel> metrics = group
                .Select(definition =>
                {
                    MetricPreference? preference = settings.Metrics
                        .FirstOrDefault(item => item.Id == definition.Id);
                    MetricSettingsViewModel metric =
                        new(definition, preference);
                    metric.SettingsChanged += Metric_SettingsChanged;
                    return metric;
                })
                .ToList();

            sections.Add(new MetricSectionViewModel(
                group.First().Section.Name,
                metrics));
        }

        MetricSections = sections;
    }

    public event EventHandler? SettingsChanged;

    public IReadOnlyList<string> LayoutOptions => AvailableLayouts;

    public IReadOnlyList<string> FontWeightOptions =>
        AvailableFontWeights;

    public IReadOnlyList<MetricSectionViewModel> MetricSections { get; }

    public string Layout
    {
        get => _layout;
        set => SetSetting(ref _layout, value ?? TelemetryDefaults.Layout);
    }

    public string HorizontalSeparator
    {
        get => _horizontalSeparator;
        set => SetSetting(ref _horizontalSeparator, value ?? string.Empty);
    }

    public double BackgroundOpacityPercent
    {
        get => _backgroundOpacityPercent;
        set
        {
            if (SetSetting(ref _backgroundOpacityPercent, value))
            {
                OnPropertyChanged(nameof(BackgroundOpacityText));
            }
        }
    }

    public string BackgroundOpacityText =>
        $"{BackgroundOpacityPercent:F0}%";

    public string FontFamily
    {
        get => _fontFamily;
        set => SetSetting(ref _fontFamily, value ?? string.Empty);
    }

    public string FontWeight
    {
        get => _fontWeight;
        set => SetSetting(
            ref _fontWeight,
            value ?? TelemetryDefaults.FontWeight);
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (SetSetting(ref _fontSize, value))
            {
                OnPropertyChanged(nameof(FontSizeText));
            }
        }
    }

    public string FontSizeText => $"{FontSize:F0}";

    public string FontColor
    {
        get => _fontColor;
        set => SetSetting(ref _fontColor, value ?? string.Empty);
    }

    /// <summary>
    /// Creates an independent persistence model from the current bindings.
    /// Preferences for temporarily missing GPUs remain intact.
    /// </summary>
    public TelemetrySettings ToSettings()
    {
        List<MetricPreference> preferences =
            [.. _hiddenPreferences.Select(ClonePreference)];
        preferences.AddRange(MetricSections
            .SelectMany(section => section.Metrics)
            .Select(metric => metric.ToPreference()));

        return new TelemetrySettings
        {
            Layout = Layout,
            HorizontalSeparator = HorizontalSeparator,
            BackgroundOpacity =
                BackgroundOpacityPercent / PercentageScale,
            FontFamily = FontFamily,
            FontWeight = FontWeight,
            FontSize = FontSize,
            FontColor = FontColor,
            Metrics = preferences,
        };
    }

    private bool SetSetting<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void Metric_SettingsChanged(object? sender, EventArgs e)
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static MetricPreference ClonePreference(
        MetricPreference preference)
    {
        return new MetricPreference
        {
            Id = preference.Id,
            IsEnabled = preference.IsEnabled,
            Format = preference.Format,
            DecimalPlaces = preference.DecimalPlaces,
        };
    }
}
