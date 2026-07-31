using System.Security.Cryptography;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace SensorHUD.Collector.Sampling.LibreHardwareMonitor;

/// <summary>
/// One cycle-local copy of the updated LibreHardwareMonitor tree. Mapping
/// code consumes these plain values and cannot re-enumerate live hardware.
/// </summary>
internal sealed record LibreHardwareMonitorSnapshot(
    LibreHardwareDeviceSnapshot? Cpu,
    IReadOnlyList<LibreHardwareDeviceSnapshot> Gpus,
    IReadOnlyList<LibreHardwareDeviceSnapshot> NetworkAdapters)
{
    public static LibreHardwareMonitorSnapshot Capture(
        IEnumerable<IHardware> hardware)
    {
        LibreHardwareDeviceSnapshot? cpu = null;
        List<LibreHardwareDeviceSnapshot> gpus = new(4);
        List<LibreHardwareDeviceSnapshot> networks = new(8);

        foreach (IHardware device in hardware)
        {
            switch (device.HardwareType)
            {
                case HardwareType.Cpu when cpu is null:
                    cpu = CaptureDevice(device);
                    break;
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                case HardwareType.GpuNvidia:
                    gpus.Add(CaptureDevice(device));
                    break;
                case HardwareType.Network:
                    networks.Add(CaptureDevice(device));
                    break;
            }
        }

        return new LibreHardwareMonitorSnapshot(cpu, gpus, networks);
    }

    private static LibreHardwareDeviceSnapshot CaptureDevice(
        IHardware hardware)
    {
        List<LibreHardwareSensorSnapshot> sensors = new(32);
        CaptureSensors(hardware, sensors);
        return new LibreHardwareDeviceSnapshot(
            StableDeviceId(hardware.Identifier.ToString()),
            hardware.Name,
            sensors);
    }

    private static void CaptureSensors(
        IHardware hardware,
        ICollection<LibreHardwareSensorSnapshot> destination)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            destination.Add(new LibreHardwareSensorSnapshot(
                sensor.SensorType,
                sensor.Name,
                sensor.Value));
        }

        foreach (IHardware child in hardware.SubHardware)
        {
            CaptureSensors(child, destination);
        }
    }

    private static string StableDeviceId(string identifier)
    {
        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(identifier));
        return Convert.ToHexString(hash)[..10];
    }
}

/// <summary>
/// Captured values for one top-level hardware device and all its children.
/// </summary>
internal sealed record LibreHardwareDeviceSnapshot(
    string DeviceId,
    string Name,
    IReadOnlyList<LibreHardwareSensorSnapshot> Sensors)
{
    /// <summary>
    /// Returns the first available sensor matching the preferred names.
    /// Type-only fallback is appropriate only when every sensor of that type
    /// is semantically valid for the requested metric.
    /// </summary>
    public double? FindFirstValue(
        SensorType type,
        IReadOnlyList<string> preferredNames,
        bool allowTypeFallback = true)
    {
        foreach (string name in preferredNames)
        {
            foreach (LibreHardwareSensorSnapshot sensor in Sensors)
            {
                if (sensor.Type == type &&
                    sensor.Value.HasValue &&
                    sensor.Name.Contains(
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return sensor.Value.Value;
                }
            }
        }

        if (!allowTypeFallback)
        {
            return null;
        }

        foreach (LibreHardwareSensorSnapshot sensor in Sensors)
        {
            if (sensor.Type == type && sensor.Value.HasValue)
            {
                return sensor.Value.Value;
            }
        }

        return null;
    }
}

/// <summary>
/// One sensor value copied after the shared hardware update pass.
/// </summary>
internal readonly record struct LibreHardwareSensorSnapshot(
    SensorType Type,
    string Name,
    double? Value);
