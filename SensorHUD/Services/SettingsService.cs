using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Shared;
using Windows.Storage;

namespace SensorHUD.Services;

/// <summary>
/// Loads and saves the single human-readable settings file. The semaphore
/// prevents the display and settings widgets from replacing it simultaneously.
/// </summary>
internal static class SettingsService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    /// <summary>
    /// Both widget definitions run in Game Bar's single app instance. This
    /// event previews every edit immediately while JSON remains the durable
    /// source of truth.
    /// </summary>
    public static event EventHandler<TelemetrySettings>? PreviewChanged;

    public static async Task<TelemetrySettings> LoadAsync()
    {
        await FileLock.WaitAsync();
        try
        {
            string path = GetLocalFilePath(CollectorProtocol.SettingsFile);
            if (!File.Exists(path))
            {
                return CreateDefaults();
            }

            string json = await File.ReadAllTextAsync(path);
            TelemetrySettings? settings = JsonSerializer.Deserialize(
                json,
                TelemetrySettingsJsonContext.Default.TelemetrySettings);

            return Normalize(settings ?? CreateDefaults());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A partially written or manually damaged file should never stop
            // the overlay. Defaults remain available through the settings UI.
            return CreateDefaults();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public static async Task SaveAsync(TelemetrySettings settings)
    {
        await FileLock.WaitAsync();
        try
        {
            settings = Normalize(settings);
            string destination = GetLocalFilePath(CollectorProtocol.SettingsFile);
            string temporary = destination + ".tmp";
            string json = JsonSerializer.Serialize(
                settings,
                TelemetrySettingsJsonContext.Default.TelemetrySettings);

            await File.WriteAllTextAsync(temporary, json);
            File.Move(temporary, destination, true);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public static TelemetrySettings CreateDefaults() => new();

    public static void Preview(TelemetrySettings settings)
    {
        PreviewChanged?.Invoke(null, Normalize(settings));
    }

    private static string GetLocalFilePath(string fileName)
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName);
    }

    private static TelemetrySettings Normalize(TelemetrySettings settings)
    {
        settings.Metrics ??= [];
        foreach (MetricPreference preference in settings.Metrics)
        {
            if (preference.DecimalPlaces is int decimalPlaces)
            {
                preference.DecimalPlaces = Math.Clamp(
                    decimalPlaces,
                    TelemetryDefaults.MinimumDecimalPlaces,
                    TelemetryDefaults.MaximumDecimalPlaces);
            }
        }

        settings.Layout = settings.Layout == LayoutNames.Horizontal
            ? LayoutNames.Horizontal
            : LayoutNames.Vertical;
        // Spacing is added by the renderer, so the setting contains only the
        // visible separator itself. An empty separator produces one space.
        settings.HorizontalSeparator = settings.HorizontalSeparator?.Trim()
            ?? TelemetryDefaults.HorizontalSeparator;
        settings.BackgroundOpacity = Math.Clamp(settings.BackgroundOpacity, 0, 1);
        settings.FontSize = Math.Clamp(
            settings.FontSize,
            TelemetryDefaults.MinimumFontSize,
            TelemetryDefaults.MaximumFontSize);
        settings.FontFamily = string.IsNullOrWhiteSpace(settings.FontFamily)
            ? TelemetryDefaults.FontFamily
            : settings.FontFamily.Trim();
        settings.FontWeight = settings.FontWeight?.Trim() switch
        {
            FontWeightNames.Light => FontWeightNames.Light,
            FontWeightNames.Normal => FontWeightNames.Normal,
            FontWeightNames.SemiBold => FontWeightNames.SemiBold,
            FontWeightNames.Bold => FontWeightNames.Bold,
            FontWeightNames.Black => FontWeightNames.Black,
            _ => TelemetryDefaults.FontWeight,
        };
        settings.FontColor = string.IsNullOrWhiteSpace(settings.FontColor)
            ? TelemetryDefaults.FontColor
            : settings.FontColor.Trim();
        return settings;
    }
}
