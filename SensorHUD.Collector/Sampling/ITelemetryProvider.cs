using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling;

/// <summary>
/// Independent source of telemetry readings. Providers report expected
/// unavailable values as readings and reserve exceptions for unexpected
/// failures.
/// </summary>
internal interface ITelemetryProvider
{
    string Name { get; }

    void Sample(ICollection<MetricReading> readings);
}
