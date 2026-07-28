using SensorHUD.Collector.Sampling.Providers;
using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling;

/// <summary>
/// Coordinates independent telemetry providers and combines their readings.
/// This sampling layer has no knowledge of process activation, IPC, or UI.
/// </summary>
internal sealed class TelemetryCollector : IDisposable
{
    private const string UnknownDeviceName = "Not detected";

    private readonly FrameMetricsProvider _frameProvider;
    private readonly ITelemetryProvider[] _providers;
    private readonly string _pawnIoStatus;

    /// <summary>
    /// Creates the provider set used for every sampling pass.
    /// </summary>
    /// <param name="pawnIoStatus">
    /// Short dependency status displayed by the settings widget.
    /// </param>
    /// <param name="hardwareAccessError">
    /// Optional error appended to protected readings when PawnIO is not ready.
    /// </param>
    public TelemetryCollector(
        string pawnIoStatus,
        string? hardwareAccessError = null)
    {
        _pawnIoStatus = pawnIoStatus;
        HardwareSensorProvider hardwareProvider =
            new(hardwareAccessError);
        _frameProvider = new FrameMetricsProvider();
        _providers = [hardwareProvider, _frameProvider];
    }

    /// <summary>
    /// Captures one point-in-time set of all available readings.
    /// </summary>
    public TelemetrySample Sample()
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

        string cpuName = UnknownDeviceName;
        HashSet<string> gpuNames =
            new(StringComparer.CurrentCultureIgnoreCase);
        foreach (TelemetryValue reading in readings)
        {
            if (cpuName == UnknownDeviceName &&
                reading.Category == MetricCategories.Cpu &&
                !string.IsNullOrWhiteSpace(reading.DeviceName))
            {
                cpuName = reading.DeviceName;
            }
            else if (reading.Category == MetricCategories.Gpu &&
                !string.IsNullOrWhiteSpace(reading.DeviceName) &&
                !string.Equals(
                    reading.DeviceName,
                    "GPU",
                    StringComparison.Ordinal))
            {
                gpuNames.Add(reading.DeviceName);
            }
        }

        return new TelemetrySample(
            readings,
            new CollectorDiagnostics
            {
                IsAdministrator = true,
                PawnIoStatus = _pawnIoStatus,
                FrameMetricsStatus = _frameProvider.Status,
                CpuName = cpuName,
                GpuNames =
                    [.. gpuNames.Order(StringComparer.CurrentCultureIgnoreCase)],
            });
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

/// <summary>
/// One provider pass before the host attaches session and transport metadata.
/// </summary>
internal sealed record TelemetrySample(
    List<TelemetryValue> Values,
    CollectorDiagnostics Diagnostics);
