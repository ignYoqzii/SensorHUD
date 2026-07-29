using Windows.Foundation;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Frontend identifiers and visual constants that are also needed by code.
/// Package.appxmanifest remains the source of Game Bar window dimensions.
/// </summary>
internal static class FrontendConstants
{
    // These activation IDs are mirrored in Package.appxmanifest.
    public const string TelemetryWidgetId = "SensorHUDWidget";
    public const string SettingsWidgetId = "SensorHUDSettings";

    public const double SettingsMinimumWidth = 480;
    public const double SettingsMinimumHeight = 400;
    public const double PercentageScale = 100;

    public static readonly Size SettingsMinimumSize =
        new(SettingsMinimumWidth, SettingsMinimumHeight);
}
