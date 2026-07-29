using System;
using SensorHUD.Core.Settings;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Owns the two process-wide services shared by Game Bar widget definitions.
/// This small composition point avoids a dependency-injection framework.
/// </summary>
internal static class AppServices
{
    public static CollectorConnection Collector { get; } = new();

    public static WidgetSettingsStore Settings { get; } = new();

    /// <summary>
    /// Raised with validated settings for immediate in-process preview before
    /// the debounced file write.
    /// </summary>
    public static event EventHandler<WidgetSettings>? SettingsPreviewed;

    public static void PreviewSettings(WidgetSettings settings) =>
        SettingsPreviewed?.Invoke(null, settings);
}
