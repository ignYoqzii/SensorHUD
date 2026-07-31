using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.LibreHardwareMonitor;

/// <summary>
/// Maps one portion of a shared LibreHardwareMonitor snapshot to registry
/// metrics. Mappers never access or update the live hardware tree.
/// </summary>
internal interface ILibreHardwareMonitorSensorMapper
{
    IReadOnlyList<ProvidedMetricDefinition> Metrics { get; }

    void Map(
        LibreHardwareMonitorSnapshot snapshot,
        IMetricSampleSink sink);
}
