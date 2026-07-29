using Windows.Foundation;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Frontend-only dimensions and visual constants. The settings dimensions are
/// mirrored in Package.appxmanifest because Game Bar reads them before the
/// page is created; keep both locations synchronized.
/// </summary>
internal static class FrontendConstants
{
    // These activation IDs are mirrored in Package.appxmanifest.
    public const string TelemetryWidgetId = "SensorHUDWidget";
    public const string SettingsWidgetId = "SensorHUDSettings";

    public const double TelemetryDefaultWidth = 420;
    public const double TelemetryDefaultHeight = 300;
    public const double TelemetryMinimumWidth = 100;
    public const double TelemetryMinimumHeight = 40;
    public const double TelemetryMaximumWidth = 1600;
    public const double TelemetryMaximumHeight = 1000;

    public const double SettingsDefaultWidth = 520;
    public const double SettingsDefaultHeight = 650;
    public const double SettingsMinimumWidth = 480;
    public const double SettingsMinimumHeight = 400;
    public const double SettingsMaximumWidth = 700;
    public const double SettingsMaximumHeight = 900;
    public const double PercentageScale = 100;

    public static readonly Size SettingsMinimumSize =
        new(SettingsMinimumWidth, SettingsMinimumHeight);
}
