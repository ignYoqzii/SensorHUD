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
}

/// <summary>
/// Display choices for one global or per-device metric instance.
/// </summary>
public sealed class MetricDisplaySettings
{
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets the chosen metric format.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the decimal count. Null uses the registry default.
    /// </summary>
    public int? Decimals { get; set; }

    /// <summary>
    /// Gets or sets the color of literal text, device names, and metric names.
    /// An empty value uses the registry default.
    /// </summary>
    public string TextColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the formatted metric value and unit. An
    /// empty value uses the registry default.
    /// </summary>
    public string ValueUnitColor { get; set; } = string.Empty;
}
