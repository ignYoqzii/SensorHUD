using System.Collections.Generic;

namespace SensorHUD.Shared;

/// <summary>
/// User-editable widget settings. Property defaults are also the reset values,
/// so adding a setting requires changing only this contract and its UI.
/// </summary>
public sealed class TelemetrySettings
{
    public string Layout { get; set; } = TelemetryDefaults.Layout;

    public string HorizontalSeparator { get; set; } = TelemetryDefaults.HorizontalSeparator;

    public double BackgroundOpacity { get; set; } = TelemetryDefaults.BackgroundOpacity;

    public string FontFamily { get; set; } = TelemetryDefaults.FontFamily;

    public string FontWeight { get; set; } = TelemetryDefaults.FontWeight;

    public double FontSize { get; set; } = TelemetryDefaults.FontSize;

    public string FontColor { get; set; } = TelemetryDefaults.FontColor;

    public List<MetricPreference> Metrics { get; set; } = [];
}

/// <summary>
/// Display choices for one metric. Preferences remain valid when a dynamic
/// device, such as a GPU, is temporarily unavailable.
/// </summary>
public sealed class MetricPreference
{
    public string Id { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public string Format { get; set; } = string.Empty;
}

public static class LayoutNames
{
    public const string Vertical = "Vertical";
    public const string Horizontal = "Horizontal";
}

public static class FontWeightNames
{
    public const string Light = "Light";
    public const string Normal = "Normal";
    public const string SemiBold = "SemiBold";
    public const string Bold = "Bold";
    public const string Black = "Black";
}

/// <summary>
/// Defaults and valid ranges for user-facing settings.
/// </summary>
public static class TelemetryDefaults
{
    public const string Layout = LayoutNames.Vertical;
    public const string HorizontalSeparator = "•";
    public const double BackgroundOpacity = 0.72;
    public const string FontFamily = "Segoe UI";
    public const string FontWeight = FontWeightNames.SemiBold;
    public const double FontSize = 18;
    public const double MinimumFontSize = 8;
    public const double MaximumFontSize = 72;
    public const string FontColor = "#FFFFFFFF";
}
