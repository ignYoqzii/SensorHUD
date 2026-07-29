using System.Collections.Generic;

namespace SensorHUD.Core.Settings;

/// <summary>
/// Determines how metric instances flow inside the telemetry widget.
/// </summary>
public enum WidgetLayout
{
    Vertical,
    Horizontal,
}

/// <summary>
/// Supported label and device-name font weights.
/// </summary>
public enum WidgetFontWeight
{
    Light,
    Normal,
    SemiBold,
    Bold,
    Black,
}

/// <summary>
/// All durable user preferences for the telemetry widget.
/// </summary>
public sealed class WidgetSettings
{
    public WidgetLayout Layout { get; set; } = SettingsDefaults.Layout;

    public string HorizontalSeparator { get; set; } =
        SettingsDefaults.HorizontalSeparator;

    public AppearanceSettings Appearance { get; set; } = new();

    /// <summary>
    /// Preferences keyed by <see cref="Metrics.MetricInstanceKey"/>.
    /// Missing keys use the corresponding registry definition's defaults.
    /// </summary>
    public Dictionary<string, MetricDisplaySettings> Metrics { get; set; } = [];
}

/// <summary>
/// Visual settings shared by every rendered metric.
/// </summary>
public sealed class AppearanceSettings
{
    public double BackgroundOpacity { get; set; } =
        SettingsDefaults.BackgroundOpacity;

    public string FontFamily { get; set; } = SettingsDefaults.FontFamily;

    public WidgetFontWeight FontWeight { get; set; } =
        SettingsDefaults.FontWeight;

    public double FontSize { get; set; } = SettingsDefaults.FontSize;

    public string FontColor { get; set; } = SettingsDefaults.FontColor;
}

/// <summary>
/// Display choices for one global or per-device metric instance.
/// </summary>
public sealed class MetricDisplaySettings
{
    public bool IsVisible { get; set; }

    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Null uses the metric definition's default precision.
    /// </summary>
    public int? Precision { get; set; }
}
