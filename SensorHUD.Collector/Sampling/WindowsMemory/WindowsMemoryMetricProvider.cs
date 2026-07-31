using SensorHUD.Collector.Transport;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.WindowsMemory;

/// <summary>
/// Publishes physical-memory values from the Windows system-memory API.
/// </summary>
internal sealed class WindowsMemoryMetricProvider : IMetricProvider
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;
    private const string DeviceName = "System Memory";

    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.Global(MetricRegistry.MemoryUsage),
        ProvidedMetricDefinition.Global(MetricRegistry.MemoryUsed),
        ProvidedMetricDefinition.Global(MetricRegistry.MemoryTotal),
    ];

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Sample(IMetricSampleSink sink)
    {
        NativeMethods.MemoryStatus memory =
            NativeMethods.MemoryStatus.Create();
        if (!NativeMethods.GlobalMemoryStatusEx(ref memory) ||
            memory.TotalPhys == 0)
        {
            return;
        }

        sink.PublishGlobal(
            MetricRegistry.MemoryUsage,
            memory.MemoryLoad,
            DeviceName);
        sink.PublishGlobal(
            MetricRegistry.MemoryUsed,
            (memory.TotalPhys - memory.AvailPhys) / BytesPerGigabyte,
            DeviceName);
        sink.PublishGlobal(
            MetricRegistry.MemoryTotal,
            memory.TotalPhys / BytesPerGigabyte,
            DeviceName);
    }
}
