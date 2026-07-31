using SensorHUD.Collector.Sampling;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Tests;

public sealed class MetricSampleSinkTests
{
    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.Global(MetricRegistry.Fps),
        ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuUsage),
    ];

    [Fact]
    public void CommitsGlobalValuesAndValueIndependentDeviceDeclarations()
    {
        MetricSampleSink sink = new(Outputs);

        sink.PublishGlobal(MetricRegistry.Fps, 120, "Frame capture");
        sink.DeclareDevice(
            MetricRegistry.GpuUsage,
            " gpu-0 ",
            " Validation GPU ");

        MetricSampleResult result = MetricSampleTestHarness.Commit(sink);

        MetricReading reading = Assert.Single(result.Readings);
        Assert.Equal(MetricRegistry.Fps, reading.MetricId);
        Assert.Equal(120, reading.Value);
        Assert.Null(reading.DeviceId);

        MetricInstance instance = Assert.Single(result.Instances);
        Assert.Equal(MetricRegistry.GpuUsage, instance.MetricId);
        Assert.Equal("gpu-0", instance.DeviceId);
        Assert.Equal("Validation GPU", instance.DeviceName);
    }

    [Fact]
    public void PublishDeviceAlsoDeclaresItsInstance()
    {
        MetricSampleSink sink = new(Outputs);

        sink.PublishDevice(
            MetricRegistry.GpuUsage,
            "gpu-0",
            "Validation GPU",
            42);

        MetricSampleResult result = MetricSampleTestHarness.Commit(sink);

        Assert.Single(result.Instances);
        MetricReading reading = Assert.Single(result.Readings);
        Assert.Equal("gpu-0", reading.DeviceId);
        Assert.Equal(42, reading.Value);
    }

    [Fact]
    public void RejectsUndeclaredWrongScopeDuplicateAndNonFiniteOutput()
    {
        MetricSampleSink sink = new(Outputs);

        Assert.Throws<InvalidOperationException>(() =>
            sink.PublishGlobal(MetricRegistry.Ping, 10));
        Assert.Throws<InvalidOperationException>(() =>
            sink.PublishGlobal(MetricRegistry.GpuUsage, 10));
        Assert.Throws<InvalidOperationException>(() =>
            sink.PublishGlobal(MetricRegistry.Fps, double.NaN));

        sink.PublishGlobal(MetricRegistry.Fps, 120);
        Assert.Throws<InvalidOperationException>(() =>
            sink.PublishGlobal(MetricRegistry.Fps, 121));
    }

    [Fact]
    public void RejectsDuplicateDeclarationsAndRegistryScopeMismatches()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new MetricSampleSink(
            [
                ProvidedMetricDefinition.Global(MetricRegistry.Fps),
                ProvidedMetricDefinition.Global(MetricRegistry.Fps),
            ]));

        Assert.Throws<InvalidOperationException>(() =>
            new MetricSampleSink(
            [
                ProvidedMetricDefinition.Global(MetricRegistry.GpuUsage),
            ]));
    }
}
