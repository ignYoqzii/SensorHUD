using SensorHUD.Collector.Bootstrap;
using SensorHUD.Collector.Sampling.DxgEtw;
using SensorHUD.Collector.Sampling.Icmp;
using SensorHUD.Collector.Sampling.LibreHardwareMonitor;
using SensorHUD.Collector.Sampling.WindowsMemory;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling;

/// <summary>
/// Coordinates independent source providers and creates transport-ready
/// snapshots. Provider registration is explicit and provider failures never
/// cross the UI-facing telemetry contract.
/// </summary>
internal sealed class TelemetrySampler : IDisposable
{
    private readonly PawnIoDependency.PawnIoResult _pawnIo;
    private readonly IMetricProvider[] _providers;
    private readonly DxgEtwMetricProvider? _frameCapture;
    private readonly string? _frameCaptureStartupError;

    private TelemetrySampler(
        PawnIoDependency.PawnIoResult pawnIo,
        IMetricProvider[] providers,
        DxgEtwMetricProvider? frameCapture,
        string? frameCaptureStartupError)
    {
        _pawnIo = pawnIo;
        _providers = providers;
        _frameCapture = frameCapture;
        _frameCaptureStartupError = frameCaptureStartupError;
    }

    /// <summary>
    /// Central, explicit production registration. Guarded construction keeps
    /// one unavailable source from suppressing unrelated providers.
    /// </summary>
    public static TelemetrySampler CreateDefault(
        PawnIoDependency.PawnIoResult pawnIo)
    {
        List<IMetricProvider> providers = [];
        HashSet<string> claimedMetrics = new(StringComparer.Ordinal);

        DxgEtwMetricProvider? frames = TryRegister(
            providers,
            claimedMetrics,
            static () => new DxgEtwMetricProvider(),
            out string? frameStartupError);
        _ = TryRegister(
            providers,
            claimedMetrics,
            static () => new LibreHardwareMonitorMetricProvider(),
            out _);
        _ = TryRegister(
            providers,
            claimedMetrics,
            static () => new WindowsMemoryMetricProvider(),
            out _);
        _ = TryRegister(
            providers,
            claimedMetrics,
            static () => new IcmpMetricProvider(),
            out _);

        return new TelemetrySampler(
            pawnIo,
            [.. providers],
            frames,
            frameStartupError);
    }

    public TelemetrySnapshot Sample()
    {
        List<MetricInstance> instances = new(16);
        List<MetricReading> readings = new(32);

        foreach (IMetricProvider provider in _providers)
        {
            try
            {
                MetricSampleSink sink = new(provider.Metrics);
                provider.Sample(sink);
                sink.CommitTo(instances, readings);
            }
            catch
            {
                // The failing source batch is discarded. Other registered
                // providers continue and no provider detail crosses the pipe.
            }
        }

        FrameCaptureSubsystemHealth frameHealth =
            _frameCapture?.CaptureHealth ??
            new(false, _frameCaptureStartupError);
        return new TelemetrySnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Instances = instances,
            Readings = readings,
            Health = new CollectorHealth
            {
                IsAdministrator = true,
                PawnIoState = _pawnIo.State,
                PawnIoVersion = _pawnIo.Version,
                PawnIoError = _pawnIo.Error,
                IsFrameCaptureActive = frameHealth.IsActive,
                FrameCaptureError = frameHealth.Error,
            },
        };
    }

    public void Dispose()
    {
        foreach (IMetricProvider provider in _providers)
        {
            if (provider is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Every independent provider still gets a chance to
                    // release its resources during process teardown.
                }
            }
        }
    }

    private static TProvider? TryRegister<TProvider>(
        ICollection<IMetricProvider> providers,
        ISet<string> claimedMetrics,
        Func<TProvider> factory,
        out string? error)
        where TProvider : class, IMetricProvider
    {
        TProvider? provider = null;
        try
        {
            provider = factory();
            _ = new MetricSampleSink(provider.Metrics);
            HashSet<string> newClaims = new(StringComparer.Ordinal);
            foreach (ProvidedMetricDefinition metric in provider.Metrics)
            {
                if (!newClaims.Add(metric.MetricId) ||
                    claimedMetrics.Contains(metric.MetricId))
                {
                    throw new InvalidOperationException(
                        $"Metric '{metric.MetricId}' has multiple providers.");
                }
            }

            foreach (string metricId in newClaims)
            {
                claimedMetrics.Add(metricId);
            }

            providers.Add(provider);
            error = null;
            return provider;
        }
        catch (Exception exception)
        {
            if (provider is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }
            }

            error = exception.Message;
            return null;
        }
    }
}
