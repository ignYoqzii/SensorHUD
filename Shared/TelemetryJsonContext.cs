using System.Text.Json.Serialization;

namespace SensorHUD.Shared;

/// <summary>
/// Compile-time JSON metadata for the live IPC protocol. Source generation is
/// required by the frontend's Native AOT build and avoids runtime reflection.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CollectorMessage))]
[JsonSerializable(typeof(TelemetrySnapshot))]
public sealed partial class TelemetryJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Settings use separate options so the durable file remains human-readable
/// without adding whitespace to every live telemetry message.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(TelemetrySettings))]
public sealed partial class TelemetrySettingsJsonContext : JsonSerializerContext
{
}
