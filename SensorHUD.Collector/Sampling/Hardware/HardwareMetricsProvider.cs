using LibreHardwareMonitor.Hardware;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Owns the single LibreHardwareMonitor <see cref="Computer"/>, performs one
/// update pass, enumerates devices, and delegates mapping to focused readers.
/// </summary>
internal sealed class HardwareMetricsProvider :
    ITelemetryProvider,
    IDisposable
{
    private static readonly UpdateVisitor Visitor = new();

    private readonly Computer _computer;
    private readonly string? _hardwareAccessError;
    private readonly string? _startupError;
    private readonly List<ISensor> _sensorBuffer = new(128);
    private readonly List<IHardware> _gpuBuffer = new(4);
    private readonly List<IHardware> _networkBuffer = new(8);

    public HardwareMetricsProvider(string? hardwareAccessError)
    {
        _hardwareAccessError = hardwareAccessError;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true,
        };

        try
        {
            _computer.Open();
        }
        catch (Exception exception)
        {
            _startupError = exception.Message;
        }
    }

    public string Name => "Hardware sensors";

    public IReadOnlyList<MetricReading> Sample()
    {
        List<MetricReading> readings = new(24);
        if (_startupError is not null)
        {
            AddGlobalUnavailable(readings, _startupError);
            return readings;
        }

        _computer.Accept(Visitor);
        _gpuBuffer.Clear();
        _networkBuffer.Clear();
        IHardware? cpu = null;
        foreach (IHardware hardware in _computer.Hardware)
        {
            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    cpu = hardware;
                    break;
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                case HardwareType.GpuNvidia:
                    _gpuBuffer.Add(hardware);
                    break;
                case HardwareType.Network:
                    _networkBuffer.Add(hardware);
                    break;
            }
        }

        CpuMetricsReader.Read(
            cpu,
            _sensorBuffer,
            readings,
            _hardwareAccessError);
        MemoryMetricsReader.Read(readings, _hardwareAccessError);
        NetworkMetricsReader.Read(
            _networkBuffer,
            _sensorBuffer,
            readings,
            _hardwareAccessError);
        foreach (IHardware gpu in _gpuBuffer)
        {
            GpuMetricsReader.Read(
                gpu,
                _sensorBuffer,
                readings,
                _hardwareAccessError);
        }

        return readings;
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

    private void AddGlobalUnavailable(
        ICollection<MetricReading> readings,
        string error)
    {
        foreach (string metricId in new[]
        {
            MetricRegistry.CpuUsage,
            MetricRegistry.CpuTemperature,
            MetricRegistry.MemoryUsage,
            MetricRegistry.MemoryUsed,
            MetricRegistry.MemoryTotal,
            MetricRegistry.NetworkSend,
            MetricRegistry.NetworkReceive,
        })
        {
            string deviceName = metricId.StartsWith(
                "cpu.",
                StringComparison.Ordinal)
                ? "CPU"
                : metricId.StartsWith(
                    "memory.",
                    StringComparison.Ordinal)
                    ? "System Memory"
                    : "Network";
            readings.Add(HardwareReading.Unavailable(
                metricId,
                deviceName,
                error,
                _hardwareAccessError));
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
