namespace SensorHUD.Core.Settings;

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
/// Determines how telemetry text is positioned across the widget.
/// </summary>
public enum WidgetHorizontalAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>
/// Determines how telemetry text is positioned down the widget.
/// </summary>
public enum WidgetVerticalAlignment
{
    Top,
    Center,
    Bottom,
}

/// <summary>
/// Visual preferences shared by every rendered metric.
/// </summary>
public sealed class AppearanceSettings
{
    /// <summary>
    /// Gets or sets the widget background opacity from zero to one.
    /// </summary>
    public double BackgroundOpacity { get; set; } =
        SettingsDefaults.BackgroundOpacity;

    /// <summary>
    /// Gets or sets the telemetry font family.
    /// </summary>
    public string FontFamily { get; set; } = SettingsDefaults.FontFamily;

    /// <summary>
    /// Gets or sets the label and device-name font weight.
    /// </summary>
    public WidgetFontWeight FontWeight { get; set; } =
        SettingsDefaults.FontWeight;

    /// <summary>
    /// Gets or sets the base telemetry font size.
    /// </summary>
    public double FontSize { get; set; } = SettingsDefaults.FontSize;

    /// <summary>
    /// Gets or sets the horizontal position of telemetry text.
    /// </summary>
    public WidgetHorizontalAlignment HorizontalTextAlignment { get; set; } =
        SettingsDefaults.HorizontalTextAlignment;

    /// <summary>
    /// Gets or sets the vertical position of telemetry text.
    /// </summary>
    public WidgetVerticalAlignment VerticalTextAlignment { get; set; } =
        SettingsDefaults.VerticalTextAlignment;
}
