using System;
using System.Collections.Generic;
using SensorHUD.Core.Settings;
using SensorHUD.Infrastructure;
using SensorHUD.Presentation;
using Windows.UI;
using Windows.UI.Xaml.Media;

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
    private Color _fontColor;

    internal AppearanceSettingsViewModel(AppearanceSettings settings)
    {
        _backgroundOpacityPercent =
            settings.BackgroundOpacity * FrontendConstants.PercentageScale;
        _fontFamily = settings.FontFamily;
        _fontWeight = settings.FontWeight;
        _fontSize = settings.FontSize;
        _fontColor = XamlTextStyle.ParseColor(settings.FontColor);
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

    /// <summary>
    /// Gets or sets the font color selected by the native UWP color picker.
    /// The value is converted back to portable ARGB text when persisted.
    /// </summary>
    public Color FontColor
    {
        get => _fontColor;
        set
        {
            if (SetSetting(ref _fontColor, value))
            {
                Notify(nameof(FontColorBrush));
                Notify(nameof(FontColorText));
            }
        }
    }

    public SolidColorBrush FontColorBrush => new(FontColor);

    public string FontColorText => XamlTextStyle.FormatColor(FontColor);

    internal void ApplyTo(WidgetSettings settings)
    {
        settings.Appearance = new AppearanceSettings
        {
            BackgroundOpacity =
                BackgroundOpacityPercent / FrontendConstants.PercentageScale,
            FontFamily = FontFamily,
            FontWeight = FontWeight,
            FontSize = FontSize,
            FontColor = XamlTextStyle.FormatColor(FontColor),
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
