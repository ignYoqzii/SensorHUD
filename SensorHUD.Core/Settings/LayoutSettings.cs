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
/// Layout preferences shared by the telemetry widget.
/// </summary>
public sealed class LayoutSettings
{
    /// <summary>
    /// Gets or sets the direction in which metrics flow.
    /// </summary>
    public WidgetLayout Direction { get; set; } =
        SettingsDefaults.LayoutDirection;

    /// <summary>
    /// Gets or sets the text placed between metrics in horizontal mode.
    /// </summary>
    public string HorizontalSeparator { get; set; } =
        SettingsDefaults.HorizontalSeparator;
}
