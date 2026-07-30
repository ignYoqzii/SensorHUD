using System;

namespace SensorHUD.Core.Settings;

/// <summary>
/// Contains every default and supported range for widget preferences.
/// </summary>
public static class SettingsDefaults
{
    public const string FileName = "settings.json";
    public const WidgetLayout LayoutDirection = WidgetLayout.Vertical;
    public const string HorizontalSeparator = "|";
    public const double BackgroundOpacity = 0.72;
    public const double MinimumBackgroundOpacity = 0;
    public const double MaximumBackgroundOpacity = 1;
    public const string FontFamily = "Segoe UI";
    public const WidgetFontWeight FontWeight = WidgetFontWeight.SemiBold;
    public const double FontSize = 18;
    public const double MinimumFontSize = 8;
    public const double MaximumFontSize = 72;
    public const WidgetHorizontalAlignment HorizontalTextAlignment =
        WidgetHorizontalAlignment.Left;
    public const WidgetVerticalAlignment VerticalTextAlignment =
        WidgetVerticalAlignment.Center;
    public static readonly TimeSpan SaveDebounce =
        TimeSpan.FromMilliseconds(350);

    /// <summary>
    /// Creates a complete independent settings object containing defaults.
    /// </summary>
    public static WidgetSettings Create() => new();
}
