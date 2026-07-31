using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.LibreHardwareMonitor;

/// <summary>
/// Sole owner of LibreHardwareMonitor access. It performs one update and one
/// capture per cycle, then supplies the resulting snapshot to explicitly
/// registered independent mappers.
/// </summary>
internal sealed class LibreHardwareMonitorMetricProvider :
    IMetricProvider,
    IDisposable
{
    private static readonly UpdateVisitor Visitor = new();

    private readonly Computer _computer;
    private readonly ILibreHardwareMonitorSensorMapper[] _mappers =
    [
        new CpuSensorMapper(),
        new GpuSensorMapper(),
        new NicThroughputSensorMapper(),
    ];
    private readonly ProvidedMetricDefinition[] _metrics;
    private readonly bool _isOpen;

    public LibreHardwareMonitorMetricProvider()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsNetworkEnabled = true,
        };

        try
        {
            _computer.Open();
            _isOpen = true;
        }
        catch
        {
        }

        _metrics = [.. _mappers.SelectMany(mapper => mapper.Metrics)];
        if (_metrics.Select(metric => metric.MetricId)
            .Distinct(StringComparer.Ordinal).Count() != _metrics.Length)
        {
            throw new InvalidOperationException(
                "LibreHardwareMonitor mappers declare duplicate metrics.");
        }
    }

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => _metrics;

    public void Sample(IMetricSampleSink sink)
    {
        if (!_isOpen)
        {
            return;
        }

        LibreHardwareMonitorSnapshot snapshot;
        try
        {
            _computer.Accept(Visitor);
            snapshot = LibreHardwareMonitorSnapshot.Capture(
                _computer.Hardware);
        }
        catch
        {
            return;
        }

        foreach (ILibreHardwareMonitorSensorMapper mapper in _mappers)
        {
            try
            {
                mapper.Map(snapshot, sink);
            }
            catch
            {
                // One mapper cannot suppress other metrics derived from the
                // same successfully captured hardware snapshot.
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _computer.Close();
        }
        catch
        {
            // Shutdown must never delay collector process exit.
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) =>
            computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware child in hardware.SubHardware)
            {
                child.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }
}
