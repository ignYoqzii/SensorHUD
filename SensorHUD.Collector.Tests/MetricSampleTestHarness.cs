using LibreHardwareMonitor.Hardware;
using SensorHUD.Collector.Sampling;
using SensorHUD.Collector.Sampling.LibreHardwareMonitor;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Tests;

/// <summary>
/// Runs real provider or mapper code against the production validating sink.
/// Contributors can reuse these helpers for synthetic source states.
/// </summary>
internal static class MetricSampleTestHarness
{
    public static MetricSampleResult Map(
        ILibreHardwareMonitorSensorMapper mapper,
        LibreHardwareMonitorSnapshot snapshot)
    {
        MetricSampleSink sink = new(mapper.Metrics);
        mapper.Map(snapshot, sink);
        return Commit(sink);
    }

    public static MetricSampleResult Sample(IMetricProvider provider)
    {
        MetricSampleSink sink = new(provider.Metrics);
        provider.Sample(sink);
        return Commit(sink);
    }

    public static LibreHardwareMonitorSnapshot Snapshot(
        LibreHardwareDeviceSnapshot? cpu = null,
        IReadOnlyList<LibreHardwareDeviceSnapshot>? gpus = null,
        IReadOnlyList<LibreHardwareDeviceSnapshot>? networkAdapters = null) =>
        new(
            cpu,
            gpus ?? [],
            networkAdapters ?? []);

    public static LibreHardwareDeviceSnapshot Device(
        string id,
        string name,
        params LibreHardwareSensorSnapshot[] sensors) =>
        new(id, name, sensors);

    public static LibreHardwareSensorSnapshot Sensor(
        SensorType type,
        string name,
        double? value) =>
        new(type, name, value);

    public static MetricSampleResult Commit(MetricSampleSink sink)
    {
        List<MetricInstance> instances = [];
        List<MetricReading> readings = [];
        sink.CommitTo(instances, readings);
        return new MetricSampleResult(instances, readings);
    }
}

internal sealed record MetricSampleResult(
    IReadOnlyList<MetricInstance> Instances,
    IReadOnlyList<MetricReading> Readings);
