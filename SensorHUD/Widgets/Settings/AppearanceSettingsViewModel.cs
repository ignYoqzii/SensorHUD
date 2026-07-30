using System;
using System.Collections.Generic;
using SensorHUD.Core.Settings;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable typography and background settings.
/// </summary>
public sealed class AppearanceSettingsViewModel : ObservableObject
{
    private static readonly WidgetFontWeight[] FontWeightChoices =
        Enum.GetValues<WidgetFontWeight>();

    private double _backgroundOpacityPercent;
    private string _fontFamily;
    private WidgetFontWeight _fontWeight;
    private double _fontSize;

    internal AppearanceSettingsViewModel(AppearanceSettings settings)
    {
        _backgroundOpacityPercent =
            settings.BackgroundOpacity * FrontendConstants.PercentageScale;
        _fontFamily = settings.FontFamily;
        _fontWeight = settings.FontWeight;
        _fontSize = settings.FontSize;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<WidgetFontWeight> FontWeightOptions =>
        FontWeightChoices;

    public double MinimumBackgroundOpacityPercent =>
        SettingsDefaults.MinimumBackgroundOpacity *
        FrontendConstants.PercentageScale;

    public double MaximumBackgroundOpacityPercent =>
        SettingsDefaults.MaximumBackgroundOpacity *
        FrontendConstants.PercentageScale;

    public double MinimumFontSize => SettingsDefaults.MinimumFontSize;

    public double MaximumFontSize => SettingsDefaults.MaximumFontSize;

    public double BackgroundOpacityPercent
    {
        get => _backgroundOpacityPercent;
        set
        {
            if (SetSetting(ref _backgroundOpacityPercent, value))
            {
                Notify(nameof(BackgroundOpacityText));
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

    public WidgetFontWeight FontWeight
    {
        get => _fontWeight;
        set => SetSetting(ref _fontWeight, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (SetSetting(ref _fontSize, value))
            {
                Notify(nameof(FontSizeText));
            }
        }
    }

    public string FontSizeText => $"{FontSize:F0}";

    internal void ApplyTo(WidgetSettings settings)
    {
        settings.Appearance = new AppearanceSettings
        {
            BackgroundOpacity =
                BackgroundOpacityPercent / FrontendConstants.PercentageScale,
            FontFamily = FontFamily,
            FontWeight = FontWeight,
            FontSize = FontSize,
        };
    }

    private bool SetSetting<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value))
        {
            return false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
