using System.Reflection;
using System.Text;
using System.Text.Json;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;
using SensorHUD.Core.Transport;

namespace SensorHUD.Core.Tests;

public sealed class TelemetryContractTests
{
    [Fact]
    public void ProviderDeclarationsExpressGlobalAndDynamicOutputs()
    {
        ProvidedMetricDefinition global =
            ProvidedMetricDefinition.Global(MetricRegistry.Fps);
        ProvidedMetricDefinition perDevice =
            ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuUsage);

        Assert.Equal(MetricScope.Global, global.Scope);
        Assert.Equal(MetricScope.PerDevice, perDevice.Scope);
    }

    [Fact]
    public void ReadingValueIsRequiredNumericData()
    {
        PropertyInfo value = Assert.Single(
            typeof(MetricReading).GetProperties(),
            property => property.Name == nameof(MetricReading.Value));

        Assert.Equal(typeof(double), value.PropertyType);
    }

    [Fact]
    public void CollectorHealthExposesCoarseFrameCaptureSignal()
    {
        Assert.NotNull(
            typeof(CollectorHealth).GetProperty(
                nameof(CollectorHealth.IsFrameCaptureActive)));
        Assert.NotNull(
            typeof(CollectorHealth).GetProperty(
                nameof(CollectorHealth.FrameCaptureError)));
    }

    [Fact]
    public void SnapshotRoundTripsInstancesSeparatelyFromReadings()
    {
        TelemetrySnapshot source = new()
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Health = new CollectorHealth
            {
                IsAdministrator = true,
                PawnIoState = PawnIoState.Ready,
                IsFrameCaptureActive = true,
            },
            Instances =
            [
                new MetricInstance
                {
                    MetricId = MetricRegistry.GpuUsage,
                    DeviceId = "gpu-0",
                    DeviceName = "GPU",
                },
            ],
            Readings =
            [
                new MetricReading
                {
                    MetricId = MetricRegistry.GpuUsage,
                    DeviceId = "gpu-0",
                    DeviceName = "GPU",
                    Value = 42,
                },
            ],
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            source,
            CollectorJsonContext.Default.TelemetrySnapshot);
        TelemetrySnapshot? result = JsonSerializer.Deserialize(
            json,
            CollectorJsonContext.Default.TelemetrySnapshot);

        MetricInstance instance = Assert.Single(result!.Instances);
        MetricReading reading = Assert.Single(result.Readings);
        Assert.Equal(instance.MetricId, reading.MetricId);
        Assert.Equal(instance.DeviceId, reading.DeviceId);
        Assert.Equal(42, reading.Value);
        Assert.DoesNotContain(
            "\"provider\"",
            Encoding.UTF8.GetString(json));
    }

    [Fact]
    public void WireProtocolVersionMatchesVersionedPipeName()
    {
        Assert.Equal(2, CollectorProtocol.Version);
        Assert.EndsWith(
            ".v2",
            CollectorProtocol.PipeName,
            StringComparison.Ordinal);
    }
}
