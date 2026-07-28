using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling.Providers;

/// <summary>
/// Small contract implemented by every backend telemetry source.
///
/// To add a source, implement this interface and register it in
/// TelemetryCollector. Providers report unsupported readings with a null Value
/// and an Error instead of throwing.
/// </summary>
internal interface ITelemetryProvider
{
    IReadOnlyList<TelemetryValue> Sample();
}
