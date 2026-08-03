using System;
using System.Net.Http;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Updates;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Owns the two process-wide services shared by Game Bar widget definitions.
/// This small composition point avoids a dependency-injection framework.
/// </summary>
internal static class AppServices
{
    private static readonly HttpClient UpdateHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static CollectorConnection Collector { get; } = new();

    public static WidgetSettingsStore Settings { get; } = new();

    public static GitHubUpdateChecker Updates { get; } =
        new(UpdateHttpClient);

    /// <summary>
    /// Raised with validated settings for immediate in-process preview before
    /// the debounced file write.
    /// </summary>
    public static event EventHandler<WidgetSettings>? SettingsPreviewed;

    public static void PreviewSettings(WidgetSettings settings) =>
        SettingsPreviewed?.Invoke(null, settings);
}
