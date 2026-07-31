using LibreHardwareMonitor.Hardware;
using SensorHUD.Collector.Sampling;
using SensorHUD.Collector.Sampling.LibreHardwareMonitor;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Tests;

public sealed class LibreHardwareMonitorSensorMapperTests
{
    [Fact]
    public void BuiltInMappersDeclareUniqueRegistryCompatibleMetrics()
    {
        ILibreHardwareMonitorSensorMapper[] mappers =
        [
            new CpuSensorMapper(),
            new GpuSensorMapper(),
            new NicThroughputSensorMapper(),
        ];

        string[] metricIds =
        [
            .. mappers.SelectMany(mapper =>
            {
                _ = new MetricSampleSink(mapper.Metrics);
                return mapper.Metrics;
            }).Select(metric => metric.MetricId),
        ];

        Assert.Equal(
            metricIds.Length,
            metricIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CpuMapperOmitsUnavailableSensors()
    {
        CpuSensorMapper mapper = new();
        LibreHardwareMonitorSnapshot noCpu =
            MetricSampleTestHarness.Snapshot();
        LibreHardwareMonitorSnapshot noValues =
            MetricSampleTestHarness.Snapshot(
                cpu: MetricSampleTestHarness.Device(
                    "cpu-0",
                    "Validation CPU",
                    MetricSampleTestHarness.Sensor(
                        SensorType.Load,
                        "CPU Total",
                        null),
                    MetricSampleTestHarness.Sensor(
                        SensorType.Load,
                        "CPU Core #1",
                        75)));

        Assert.Empty(
            MetricSampleTestHarness.Map(mapper, noCpu).Readings);
        Assert.Empty(
            MetricSampleTestHarness.Map(mapper, noValues).Readings);
    }

    [Fact]
    public void CpuMapperPublishesAvailableLoadAndPackageTemperature()
    {
        CpuSensorMapper mapper = new();
        LibreHardwareDeviceSnapshot cpu = MetricSampleTestHarness.Device(
            "cpu-0",
            "Validation CPU",
            MetricSampleTestHarness.Sensor(
                SensorType.Load,
                "CPU Total",
                55),
            MetricSampleTestHarness.Sensor(
                SensorType.Temperature,
                "CPU Package",
                68));

        MetricSampleResult result = MetricSampleTestHarness.Map(
            mapper,
            MetricSampleTestHarness.Snapshot(cpu: cpu));

        Assert.Collection(
            result.Readings.OrderBy(reading => reading.MetricId),
            reading =>
            {
                Assert.Equal(MetricRegistry.CpuTemperature, reading.MetricId);
                Assert.Equal(68, reading.Value);
            },
            reading =>
            {
                Assert.Equal(MetricRegistry.CpuUsage, reading.MetricId);
                Assert.Equal(55, reading.Value);
            });
    }

    [Fact]
    public void GpuMapperDeclaresEveryMetricForEveryDetectedAdapter()
    {
        GpuSensorMapper mapper = new();
        LibreHardwareDeviceSnapshot integrated =
            MetricSampleTestHarness.Device(
                "gpu-integrated",
                "Integrated GPU");
        LibreHardwareDeviceSnapshot discrete =
            MetricSampleTestHarness.Device(
                "gpu-discrete",
                "Discrete GPU");

        MetricSampleResult result = MetricSampleTestHarness.Map(
            mapper,
            MetricSampleTestHarness.Snapshot(
                gpus: [integrated, discrete]));

        Assert.Empty(result.Readings);
        Assert.Equal(mapper.Metrics.Count * 2, result.Instances.Count);
        Assert.All(
            mapper.Metrics,
            output =>
            {
                Assert.Contains(
                    result.Instances,
                    instance =>
                        instance.MetricId == output.MetricId &&
                        instance.DeviceId == integrated.DeviceId);
                Assert.Contains(
                    result.Instances,
                    instance =>
                        instance.MetricId == output.MetricId &&
                        instance.DeviceId == discrete.DeviceId);
            });
    }

    [Fact]
    public void GpuMapperHandlesMixedSensorShapesIndependently()
    {
        GpuSensorMapper mapper = new();
        LibreHardwareDeviceSnapshot integrated =
            MetricSampleTestHarness.Device(
                "gpu-integrated",
                "Integrated GPU",
                MetricSampleTestHarness.Sensor(
                    SensorType.Load,
                    "D3D 3D",
                    25));
        LibreHardwareDeviceSnapshot discrete =
            MetricSampleTestHarness.Device(
                "gpu-discrete",
                "Discrete GPU",
                MetricSampleTestHarness.Sensor(
                    SensorType.Load,
                    "GPU Core",
                    80),
                MetricSampleTestHarness.Sensor(
                    SensorType.Temperature,
                    "GPU Core",
                    70),
                MetricSampleTestHarness.Sensor(
                    SensorType.SmallData,
                    "GPU Memory Used",
                    4096),
                MetricSampleTestHarness.Sensor(
                    SensorType.SmallData,
                    "GPU Memory Total",
                    8192));

        MetricSampleResult result = MetricSampleTestHarness.Map(
            mapper,
            MetricSampleTestHarness.Snapshot(
                gpus: [integrated, discrete]));

        Assert.Equal(6, result.Readings.Count);
        Assert.Equal(
            25,
            FindReading(
                result,
                MetricRegistry.GpuUsage,
                integrated.DeviceId).Value);
        Assert.Equal(
            50,
            FindReading(
                result,
                MetricRegistry.GpuVramUsage,
                discrete.DeviceId).Value);
        Assert.Equal(
            4,
            FindReading(
                result,
                MetricRegistry.GpuVramUsed,
                discrete.DeviceId).Value);
        Assert.Equal(
            8,
            FindReading(
                result,
                MetricRegistry.GpuVramTotal,
                discrete.DeviceId).Value);
    }

    [Fact]
    public void NicMapperAggregatesOnlyAvailableAdapterDirections()
    {
        NicThroughputSensorMapper mapper = new();
        LibreHardwareDeviceSnapshot ethernet =
            MetricSampleTestHarness.Device(
                "nic-ethernet",
                "Ethernet",
                MetricSampleTestHarness.Sensor(
                    SensorType.Throughput,
                    "Upload Speed",
                    1_000_000),
                MetricSampleTestHarness.Sensor(
                    SensorType.Throughput,
                    "Download Speed",
                    2_000_000));
        LibreHardwareDeviceSnapshot wifi =
            MetricSampleTestHarness.Device(
                "nic-wifi",
                "Wi-Fi",
                MetricSampleTestHarness.Sensor(
                    SensorType.Throughput,
                    "Upload",
                    500_000));

        MetricSampleResult result = MetricSampleTestHarness.Map(
            mapper,
            MetricSampleTestHarness.Snapshot(
                networkAdapters: [ethernet, wifi]));

        Assert.Equal(12, FindGlobal(
            result,
            MetricRegistry.NetworkSend).Value);
        Assert.Equal(16, FindGlobal(
            result,
            MetricRegistry.NetworkReceive).Value);
    }

    [Fact]
    public void NicMapperOmitsMetricsWhenNoThroughputSensorsExist()
    {
        NicThroughputSensorMapper mapper = new();
        LibreHardwareDeviceSnapshot adapter =
            MetricSampleTestHarness.Device(
                "nic-0",
                "Validation NIC");

        MetricSampleResult result = MetricSampleTestHarness.Map(
            mapper,
            MetricSampleTestHarness.Snapshot(
                networkAdapters: [adapter]));

        Assert.Empty(result.Readings);
    }

    private static MetricReading FindReading(
        MetricSampleResult result,
        string metricId,
        string deviceId) =>
        Assert.Single(
            result.Readings,
            reading =>
                reading.MetricId == metricId &&
                reading.DeviceId == deviceId);

    private static MetricReading FindGlobal(
        MetricSampleResult result,
        string metricId) =>
        Assert.Single(
            result.Readings,
            reading =>
                reading.MetricId == metricId &&
                reading.DeviceId is null);
}
