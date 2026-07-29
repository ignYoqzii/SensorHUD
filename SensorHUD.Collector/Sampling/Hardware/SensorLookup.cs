using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace SensorHUD.Collector.Sampling.Hardware;

/// <summary>
/// Centralizes recursive sensor traversal, vendor-name preference matching,
/// and stable device identifiers.
/// </summary>
internal static class SensorLookup
{
    private static readonly ConcurrentDictionary<string, string> DeviceIds =
        new(StringComparer.Ordinal);

    public static void BufferAll(
        IHardware hardware,
        List<ISensor> destination)
    {
        destination.Clear();
        AddRecursive(hardware, destination);
    }

    public static double? FirstValue(
        IReadOnlyList<ISensor> sensors,
        SensorType type,
        IReadOnlyList<string> preferredNames)
    {
        foreach (string name in preferredNames)
        {
            foreach (ISensor sensor in sensors)
            {
                if (sensor.SensorType == type &&
                    sensor.Value.HasValue &&
                    sensor.Name.Contains(
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return sensor.Value.Value;
                }
            }
        }

        foreach (ISensor sensor in sensors)
        {
            if (sensor.SensorType == type && sensor.Value.HasValue)
            {
                return sensor.Value.Value;
            }
        }

        return null;
    }

    public static string StableDeviceId(IHardware hardware) =>
        DeviceIds.GetOrAdd(
            hardware.Identifier.ToString(),
            static identifier =>
            {
                byte[] hash = SHA256.HashData(
                    Encoding.UTF8.GetBytes(identifier));
                return Convert.ToHexString(hash)[..10];
            });

    private static void AddRecursive(
        IHardware hardware,
        ICollection<ISensor> destination)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            destination.Add(sensor);
        }

        foreach (IHardware child in hardware.SubHardware)
        {
            AddRecursive(child, destination);
        }
    }
}
