using LibreHardwareMonitor.Hardware;
using SensorHUD.Collector.Sampling.Network;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Owns the single LibreHardwareMonitor <see cref="Computer"/>, performs one
/// guarded update pass, enumerates devices, and delegates mapping to focused
/// readers. Sources that do not depend on LibreHardwareMonitor remain
/// available when its startup or sampling fails.
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

    public void Sample(ICollection<MetricReading> readings)
    {
        _gpuBuffer.Clear();
        _networkBuffer.Clear();
        IHardware? cpu = null;
        string? monitorError = _startupError;
        if (_startupError is null)
        {
            try
            {
                _computer.Accept(Visitor);
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
            }
            catch (Exception exception)
            {
                monitorError = exception.Message;
                cpu = null;
                _gpuBuffer.Clear();
                _networkBuffer.Clear();
            }
        }

        string? hardwareError = CombineErrors(
            monitorError,
            _hardwareAccessError);
        CpuMetricsReader.Read(
            cpu,
            _sensorBuffer,
            readings,
            hardwareError);
        MemoryMetricsReader.Read(readings);
        AdapterThroughputMetricsReader.Read(
            _networkBuffer,
            _sensorBuffer,
            readings,
            monitorError);
        foreach (IHardware gpu in _gpuBuffer)
        {
            GpuMetricsReader.Read(
                gpu,
                _sensorBuffer,
                readings,
                _hardwareAccessError);
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
        finally
        {
            _sensorBuffer.Clear();
            _gpuBuffer.Clear();
            _networkBuffer.Clear();
        }
    }

    private static string? CombineErrors(string? first, string? second) =>
        string.IsNullOrWhiteSpace(first)
            ? second
            : string.IsNullOrWhiteSpace(second)
                ? first
                : $"{first} {second}";

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
