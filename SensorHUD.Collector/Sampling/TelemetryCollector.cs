using SensorHUD.Collector.Sampling.Providers;
using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling;

/// <summary>
/// Coordinates independent telemetry providers and combines their readings.
/// This sampling layer has no knowledge of process activation, IPC, or UI.
/// </summary>
/// <param name="hardwareAccessError">
/// Optional explanation used when the host could not prepare privileged
/// hardware access, such as PawnIO. Public metrics continue to work.
/// </param>
internal sealed class TelemetryCollector(string? hardwareAccessError = null) : IDisposable
{
    private readonly ITelemetryProvider[] _providers =
        [
            new HardwareSensorProvider(hardwareAccessError),
            new FrameMetricsProvider(),
        ];

    /// <summary>
    /// Captures one point-in-time set of all available readings.
    /// </summary>
    public List<TelemetryValue> Sample()
    {
        List<TelemetryValue> readings = [];

        foreach (ITelemetryProvider provider in _providers)
        {
            try
            {
                readings.AddRange(provider.Sample());
            }
            catch
            {
                // Providers normally return N/A for unsupported readings.
                // This boundary prevents one unexpected provider failure from
                // suppressing every independent source.
            }
        }

        return readings;
    }

    public void Dispose()
    {
        foreach (ITelemetryProvider provider in _providers)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
