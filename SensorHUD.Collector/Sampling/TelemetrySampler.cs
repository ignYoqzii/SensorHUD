using SensorHUD.Collector.Bootstrap;
using SensorHUD.Collector.Sampling.Frames;
using SensorHUD.Collector.Sampling.Hardware;
using SensorHUD.Collector.Sampling.Network;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling;

/// <summary>
/// Coordinates independent providers and creates transport-ready snapshots.
/// It has no knowledge of process activation, named pipes, or XAML.
/// </summary>
internal sealed class TelemetrySampler : IDisposable
{
    private readonly PawnIoDependency.PawnIoResult _pawnIo;
    private readonly FrameMetricsProvider _frames;
    private readonly ITelemetryProvider[] _providers;

    private TelemetrySampler(
        PawnIoDependency.PawnIoResult pawnIo,
        FrameMetricsProvider frames,
        params ITelemetryProvider[] providers)
    {
        _pawnIo = pawnIo;
        _frames = frames;
        _providers = providers;
    }

    /// <summary>
    /// Registers the production hardware sources in one obvious location.
    /// Adding a source requires implementing <see cref="ITelemetryProvider"/>
    /// and adding it to this list.
    /// </summary>
    public static TelemetrySampler CreateDefault(
        PawnIoDependency.PawnIoResult pawnIo)
    {
        HardwareMetricsProvider hardware =
            new(pawnIo.Error);
        FrameMetricsProvider frames = new();
        InternetConnectionMetricsProvider internetConnection = new();
        return new TelemetrySampler(
            pawnIo,
            frames,
            hardware,
            frames,
            internetConnection);
    }

    public TelemetrySnapshot Sample()
    {
        List<MetricReading> readings = new(32);
        string? lastProviderError = null;

        foreach (ITelemetryProvider provider in _providers)
        {
            try
            {
                readings.AddRange(provider.Sample());
            }
            catch (Exception exception)
            {
                // One unexpected source failure is reported in health but does
                // not suppress independent hardware or frame sources.
                lastProviderError =
                    $"{provider.Name}: {exception.Message}";
            }
        }

        FrameProviderStatus frameStatus = _frames.Status;
        return new TelemetrySnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Readings = readings,
            Health = new CollectorHealth
            {
                IsAdministrator = true,
                PawnIoState = _pawnIo.State,
                PawnIoVersion = _pawnIo.Version,
                PawnIoError = _pawnIo.Error,
                FrameCaptureState = frameStatus.State,
                ForegroundProcess = frameStatus.TargetProcess,
                FrameCaptureError = frameStatus.Error,
                LastProviderError = lastProviderError,
            },
        };
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
