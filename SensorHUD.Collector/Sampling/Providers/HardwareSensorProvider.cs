using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LibreHardwareMonitor.Hardware;
using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling.Providers;

internal sealed partial class HardwareSensorProvider : ITelemetryProvider, IDisposable
{
    private static readonly UpdateVisitor Visitor = new();
    private static readonly ConcurrentDictionary<string, string> DeviceIdCache = new();

    private readonly Computer _computer;
    private readonly string? _startupError;
    private readonly string? _hardwareAccessError;

    // Reusable buffer to prevent allocating lists/arrays on every hardware scan
    private readonly List<ISensor> _sensorBuffer = new(128);

    public HardwareSensorProvider(string? hardwareAccessError = null)
    {
        _hardwareAccessError = hardwareAccessError;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true
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

    public IReadOnlyList<TelemetryValue> Sample()
    {
        List<TelemetryValue> values = new(32);

        if (_startupError is not null)
        {
            AddStartupErrors(values, _startupError);
            return values;
        }

        try
        {
            _computer.Accept(Visitor);

            IHardware? cpu = null;
            List<IHardware> gpus = new(4);
            List<IHardware> networks = new(4);

            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu) cpu = hardware;
                else if (hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia) gpus.Add(hardware);
                else if (hardware.HardwareType == HardwareType.Network) networks.Add(hardware);
            }

            AddCpu(values, cpu);
            AddMemory(values); // Memory from LibreHardwareMonitorLib is unreliable, so we use Windows memory interop instead
            AddNetwork(values, networks);

            if (gpus.Count == 0)
            {
                AddUnavailableGpu(values, "No supported GPU sensor was found.");
            }
            else
            {
                foreach (IHardware gpu in gpus)
                {
                    AddGpu(values, gpu);
                }
            }
        }
        catch (Exception exception)
        {
            values.Clear();
            AddStartupErrors(values, exception.Message);
        }

        return values;
    }

    private void AddStartupErrors(List<TelemetryValue> values, string error)
    {
        values.Add(UnavailableValue(MetricIds.CpuUsage, "CPU usage", MetricCategories.Cpu, MetricUnits.Percent, error, "CPU"));
        values.Add(UnavailableValue(MetricIds.CpuTemperature, "CPU temperature", MetricCategories.Cpu, MetricUnits.Celsius, error, "CPU"));
        values.Add(UnavailableValue(MetricIds.RamUsage, "RAM usage", MetricCategories.Ram, MetricUnits.Percent, error, "Memory"));
        AddUnavailableGpu(values, error);
        AddUnavailableNetwork(values, error);
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { /* Ignore on shutdown */ }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware child in hardware.SubHardware) child.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
