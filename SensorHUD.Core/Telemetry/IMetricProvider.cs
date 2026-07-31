using System.Collections.Generic;
using SensorHUD.Core.Metrics;

namespace SensorHUD.Core.Telemetry;

/// <summary>
/// Independent source of registry metric values. Providers declare their
/// output shape and publish only currently available numeric readings.
/// </summary>
public interface IMetricProvider
{
    IReadOnlyList<ProvidedMetricDefinition> Metrics { get; }

    void Sample(IMetricSampleSink sink);
}

/// <summary>
/// Declares one registry metric produced by a provider. The registry remains
/// authoritative; collector registration validates this declaration against
/// the registered metric scope.
/// </summary>
public readonly record struct ProvidedMetricDefinition(
    string MetricId,
    MetricScope Scope)
{
    public static ProvidedMetricDefinition Global(string metricId) =>
        new(metricId, MetricScope.Global);

    public static ProvidedMetricDefinition PerDevice(string metricId) =>
        new(metricId, MetricScope.PerDevice);
}

/// <summary>
/// Receives validated provider output without exposing transport or category
/// aggregation to providers.
/// </summary>
public interface IMetricSampleSink
{
    void PublishGlobal(
        string metricId,
        double value,
        string? deviceName = null);

    void DeclareDevice(
        string metricId,
        string deviceId,
        string? deviceName = null);

    void PublishDevice(
        string metricId,
        string deviceId,
        string? deviceName,
        double value);
}
