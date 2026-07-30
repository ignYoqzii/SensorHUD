using System;
using System.Collections.Generic;
using SensorHUD.Core.Settings;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Bindable background, typography, and text-alignment settings.
/// </summary>
public sealed class AppearanceSettingsViewModel : EditableSettingsViewModel
{
    private static readonly WidgetFontWeight[] FontWeightChoices =
        Enum.GetValues<WidgetFontWeight>();
    private static readonly WidgetHorizontalAlignment[]
        HorizontalAlignmentChoices =
            Enum.GetValues<WidgetHorizontalAlignment>();
    private static readonly WidgetVerticalAlignment[]
        VerticalAlignmentChoices =
            Enum.GetValues<WidgetVerticalAlignment>();

    private double _backgroundOpacityPercent;
    private string _fontFamily;
    private WidgetFontWeight _fontWeight;
    private double _fontSize;
    private WidgetHorizontalAlignment _horizontalTextAlignment;
    private WidgetVerticalAlignment _verticalTextAlignment;

    internal AppearanceSettingsViewModel(AppearanceSettings settings)
    {
        _backgroundOpacityPercent =
            settings.BackgroundOpacity * FrontendConstants.PercentageScale;
        _fontFamily = settings.FontFamily;
        _fontWeight = settings.FontWeight;
        _fontSize = settings.FontSize;
        _horizontalTextAlignment = settings.HorizontalTextAlignment;
        _verticalTextAlignment = settings.VerticalTextAlignment;
    }

    public IReadOnlyList<WidgetFontWeight> FontWeightOptions =>
        FontWeightChoices;

    public IReadOnlyList<WidgetHorizontalAlignment>
        HorizontalTextAlignmentOptions => HorizontalAlignmentChoices;

    public IReadOnlyList<WidgetVerticalAlignment>
        VerticalTextAlignmentOptions => VerticalAlignmentChoices;

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

    public WidgetHorizontalAlignment HorizontalTextAlignment
    {
        get => _horizontalTextAlignment;
        set => SetSetting(ref _horizontalTextAlignment, value);
    }

    public WidgetVerticalAlignment VerticalTextAlignment
    {
        get => _verticalTextAlignment;
        set => SetSetting(ref _verticalTextAlignment, value);
    }

    internal void ApplyTo(WidgetSettings settings)
    {
        settings.Appearance = new AppearanceSettings
        {
            BackgroundOpacity =
                BackgroundOpacityPercent / FrontendConstants.PercentageScale,
            FontFamily = FontFamily,
            FontWeight = FontWeight,
            FontSize = FontSize,
            HorizontalTextAlignment = HorizontalTextAlignment,
            VerticalTextAlignment = VerticalTextAlignment,
        };
    }
}
