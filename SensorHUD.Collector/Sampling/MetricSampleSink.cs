using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling;

/// <summary>
/// Validates and buffers one provider sample. A failed provider never commits
/// partial output into the collector snapshot.
/// </summary>
internal sealed class MetricSampleSink : IMetricSampleSink
{
    private readonly Dictionary<string, MetricScope> _metrics;
    private readonly HashSet<(string MetricId, string DeviceId)> _instances =
        [];
    private readonly HashSet<(string MetricId, string? DeviceId)> _readings =
        [];
    private readonly List<MetricInstance> _instanceBuffer = [];
    private readonly List<MetricReading> _readingBuffer = [];

    public MetricSampleSink(
        IReadOnlyList<ProvidedMetricDefinition> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        _metrics = new Dictionary<string, MetricScope>(
            metrics.Count,
            StringComparer.Ordinal);
        foreach (ProvidedMetricDefinition metric in metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.MetricId))
            {
                throw new InvalidOperationException(
                    "Provider metric IDs cannot be empty.");
            }

            MetricDefinition definition = MetricRegistry.Get(metric.MetricId);
            if (definition.Scope != metric.Scope)
            {
                throw new InvalidOperationException(
                    $"Provider scope for '{metric.MetricId}' does not match " +
                    "the metric registry.");
            }

            if (!_metrics.TryAdd(metric.MetricId, metric.Scope))
            {
                throw new InvalidOperationException(
                    $"Provider metric '{metric.MetricId}' is duplicated.");
            }
        }
    }

    public void PublishGlobal(
        string metricId,
        double value,
        string? deviceName = null)
    {
        ValidateMetric(metricId, MetricScope.Global);
        ValidateValue(metricId, value);
        if (!_readings.Add((metricId, null)))
        {
            throw new InvalidOperationException(
                $"Provider published global metric '{metricId}' more than once.");
        }

        _readingBuffer.Add(new MetricReading
        {
            MetricId = metricId,
            DeviceName = NormalizeOptional(deviceName),
            Value = value,
        });
    }

    public void DeclareDevice(
        string metricId,
        string deviceId,
        string? deviceName = null)
    {
        ValidateMetric(metricId, MetricScope.PerDevice);
        string normalizedId = NormalizeDeviceId(metricId, deviceId);
        if (!_instances.Add((metricId, normalizedId)))
        {
            return;
        }

        _instanceBuffer.Add(new MetricInstance
        {
            MetricId = metricId,
            DeviceId = normalizedId,
            DeviceName = NormalizeOptional(deviceName),
        });
    }

    public void PublishDevice(
        string metricId,
        string deviceId,
        string? deviceName,
        double value)
    {
        ValidateValue(metricId, value);
        DeclareDevice(metricId, deviceId, deviceName);
        string normalizedId = NormalizeDeviceId(metricId, deviceId);
        if (!_readings.Add((metricId, normalizedId)))
        {
            throw new InvalidOperationException(
                $"Provider published metric '{metricId}' more than once for " +
                $"device '{normalizedId}'.");
        }

        _readingBuffer.Add(new MetricReading
        {
            MetricId = metricId,
            DeviceId = normalizedId,
            DeviceName = NormalizeOptional(deviceName),
            Value = value,
        });
    }

    public void CommitTo(
        ICollection<MetricInstance> instances,
        ICollection<MetricReading> readings)
    {
        foreach (MetricInstance instance in _instanceBuffer)
        {
            instances.Add(instance);
        }

        foreach (MetricReading reading in _readingBuffer)
        {
            readings.Add(reading);
        }
    }

    private void ValidateMetric(string metricId, MetricScope expectedScope)
    {
        if (!_metrics.TryGetValue(metricId, out MetricScope scope))
        {
            throw new InvalidOperationException(
                $"Provider published undeclared metric '{metricId}'.");
        }

        if (scope != expectedScope)
        {
            throw new InvalidOperationException(
                $"Provider used the wrong publication scope for '{metricId}'.");
        }
    }

    private static void ValidateValue(string metricId, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException(
                $"Provider published a non-finite value for '{metricId}'.");
        }
    }

    private static string NormalizeDeviceId(
        string metricId,
        string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new InvalidOperationException(
                $"Per-device metric '{metricId}' requires a device ID.");
        }

        return deviceId.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
