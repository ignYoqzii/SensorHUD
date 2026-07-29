using System.Text.Json.Serialization;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Core.Transport;

/// <summary>
/// Native-AOT-compatible JSON metadata for live collector messages.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CollectorMessage))]
[JsonSerializable(typeof(TelemetrySnapshot))]
public sealed partial class CollectorJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Native-AOT-compatible, human-readable JSON metadata for settings.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(WidgetSettings))]
public sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
