using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Transport;
using Windows.Storage;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Loads, validates, and atomically saves the one durable settings file.
/// Preview and debounce behavior intentionally live outside this class.
/// </summary>
internal sealed class WidgetSettingsStore
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public async Task<WidgetSettings> LoadAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return SettingsDefaults.Create();
            }

            string json = await File.ReadAllTextAsync(path);
            WidgetSettings? settings = JsonSerializer.Deserialize(
                json,
                SettingsJsonContext.Default.WidgetSettings);
            return SettingsValidator.Normalize(settings);
        }
        catch (Exception exception)
            when (exception is IOException or
                UnauthorizedAccessException or
                JsonException)
        {
            return SettingsDefaults.Create();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(WidgetSettings settings)
    {
        WidgetSettings normalized = SettingsValidator.Normalize(settings);
        await _fileLock.WaitAsync();
        try
        {
            string destination = GetPath();
            string temporary = destination + ".tmp";
            string json = JsonSerializer.Serialize(
                normalized,
                SettingsJsonContext.Default.WidgetSettings);
            await File.WriteAllTextAsync(temporary, json);
            File.Move(temporary, destination, true);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static string GetPath() => Path.Combine(
        ApplicationData.Current.LocalFolder.Path,
        SettingsDefaults.FileName);
}
