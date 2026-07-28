using LibreHardwareMonitor.Hardware;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SensorHUD.Shared;

namespace SensorHUD.Collector.Sampling.Providers;

internal sealed partial class HardwareSensorProvider
{
    private const double BytesToMegabits = 8.0 / 1_000_000.0;
    private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;
    private const double MegabytesPerGigabyte = 1024.0;

    // Sensor names are checked in priority order because vendors expose the
    // same physical reading under slightly different labels.
    private static readonly string[] CpuTotalLoad = ["CPU Total", "Total"];
    private static readonly string[] GpuCoreLoad = ["GPU Core", "D3D 3D", "GPU Total"];
    private static readonly string[] GpuCoreTemp = ["GPU Core", "GPU"];
    private static readonly string[] GpuVramLoad = ["GPU Memory", "Memory"];
    private static readonly string[] GpuVramUsed = ["GPU Memory Used", "Memory Used"];
    private static readonly string[] GpuVramTotal = ["GPU Memory Total", "Memory Total"];
    private static readonly string[] NetworkUpload = ["Upload Speed", "Upload"];
    private static readonly string[] NetworkDownload = ["Download Speed", "Download"];

    private void AddCpu(List<TelemetryValue> values, IHardware? cpu)
    {
        string deviceName = cpu?.Name ?? "CPU";
        if (cpu is null)
        {
            const string error = "No supported CPU sensor was found.";
            values.Add(UnavailableValue(
                MetricIds.CpuUsage,
                "CPU usage",
                MetricCategories.Cpu,
                MetricUnits.Percent,
                error,
                deviceName));
            values.Add(UnavailableValue(
                MetricIds.CpuTemperature,
                "CPU temperature",
                MetricCategories.Cpu,
                MetricUnits.Celsius,
                error,
                deviceName));
            return;
        }

        BufferAllSensors(cpu);

        double? usage = GetFirstValue(
            _sensorBuffer,
            SensorType.Load,
            CpuTotalLoad);
        values.Add(usage is null
            ? UnavailableValue(
                MetricIds.CpuUsage,
                "CPU usage",
                MetricCategories.Cpu,
                MetricUnits.Percent,
                "CPU load sensors returned no total value.",
                deviceName)
            : Value(
                MetricIds.CpuUsage,
                "CPU usage",
                MetricCategories.Cpu,
                MetricUnits.Percent,
                usage,
                deviceName: deviceName));

        double? temperature = null;
        ISensor? package = null;
        double maxTemp = double.MinValue;
        bool foundTemp = false;

        foreach (ISensor sensor in _sensorBuffer)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                if (sensor.Name.Contains(
                    "Package",
                    StringComparison.OrdinalIgnoreCase))
                {
                    package = sensor;
                }

                if (sensor.Value.HasValue)
                {
                    if (sensor.Value.Value > maxTemp)
                    {
                        maxTemp = sensor.Value.Value;
                    }

                    foundTemp = true;
                }
            }
        }

        if (package?.Value is not null)
        {
            temperature = package.Value;
        }
        else if (foundTemp)
        {
            temperature = maxTemp;
        }

        values.Add(temperature is null
            ? UnavailableValue(
                MetricIds.CpuTemperature,
                "CPU temperature",
                MetricCategories.Cpu,
                MetricUnits.Celsius,
                "LibreHardwareMonitor returned no CPU temperature value.",
                deviceName)
            : Value(
                MetricIds.CpuTemperature,
                "CPU temperature",
                MetricCategories.Cpu,
                MetricUnits.Celsius,
                temperature,
                deviceName: deviceName));
    }

    private void AddMemory(List<TelemetryValue> values)
    {
        string deviceName = "System Memory";
        if (!TryGetWindowsMemoryUsage(
            out double usage,
            out double usedGigabytes,
            out double totalGigabytes))
        {
            const string error =
                "Failed to query Windows system memory status.";
            values.Add(UnavailableValue(
                MetricIds.RamUsage,
                "RAM usage",
                MetricCategories.Ram,
                MetricUnits.Percent,
                error,
                deviceName));
            values.Add(UnavailableValue(
                MetricIds.RamUsed,
                "RAM used",
                MetricCategories.Ram,
                MetricUnits.Gigabytes,
                error,
                deviceName));
            values.Add(UnavailableValue(
                MetricIds.RamTotal,
                "RAM total",
                MetricCategories.Ram,
                MetricUnits.Gigabytes,
                error,
                deviceName));
            return;
        }

        values.Add(Value(
            MetricIds.RamUsage,
            "RAM usage",
            MetricCategories.Ram,
            MetricUnits.Percent,
            usage,
            deviceName: deviceName));
        values.Add(Value(
            MetricIds.RamUsed,
            "RAM used",
            MetricCategories.Ram,
            MetricUnits.Gigabytes,
            usedGigabytes,
            deviceName: deviceName));
        values.Add(Value(
            MetricIds.RamTotal,
            "RAM total",
            MetricCategories.Ram,
            MetricUnits.Gigabytes,
            totalGigabytes,
            deviceName: deviceName));
    }

    private static bool TryGetWindowsMemoryUsage(
        out double usagePercent,
        out double usedGigabytes,
        out double totalGigabytes)
    {
        usagePercent = 0;
        usedGigabytes = 0;
        totalGigabytes = 0;

        MemoryStatusEx memory = new();
        if (!GlobalMemoryStatusEx(memory) || memory.TotalPhys == 0)
        {
            return false;
        }

        usagePercent = memory.MemoryLoad;
        totalGigabytes = memory.TotalPhys / BytesPerGigabyte;
        usedGigabytes =
            (memory.TotalPhys - memory.AvailPhys) / BytesPerGigabyte;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(
        [In, Out] MemoryStatusEx lpBuffer);

    private void AddGpu(List<TelemetryValue> values, IHardware gpu)
    {
        string deviceId = StableDeviceId(gpu.Identifier.ToString());
        BufferAllSensors(gpu);

        double? usage = GetFirstValue(
            _sensorBuffer,
            SensorType.Load,
            GpuCoreLoad);
        double? temperature = GetFirstValue(
            _sensorBuffer,
            SensorType.Temperature,
            GpuCoreTemp);
        double? vram = GetFirstValue(
            _sensorBuffer,
            SensorType.Load,
            GpuVramLoad);
        double? usedMegabytes = GetFirstValue(
            _sensorBuffer,
            SensorType.SmallData,
            GpuVramUsed);
        double? totalMegabytes = GetFirstValue(
            _sensorBuffer,
            SensorType.SmallData,
            GpuVramTotal);

        if (vram is null && usedMegabytes is not null && totalMegabytes > 0)
        {
            vram = (usedMegabytes / totalMegabytes) * 100;
        }

        values.Add(GpuValue(
            MetricIds.ForGpu(deviceId, MetricIds.UsageSuffix),
            "GPU usage",
            gpu.Name,
            MetricUnits.Percent,
            usage));
        values.Add(GpuValue(
            MetricIds.ForGpu(deviceId, MetricIds.TemperatureSuffix),
            "GPU temperature",
            gpu.Name,
            MetricUnits.Celsius,
            temperature));
        values.Add(GpuValue(
            MetricIds.ForGpu(deviceId, MetricIds.VramSuffix),
            "VRAM usage",
            gpu.Name,
            MetricUnits.Percent,
            vram));
        values.Add(GpuValue(
            MetricIds.ForGpu(deviceId, MetricIds.VramUsedSuffix),
            "VRAM used",
            gpu.Name,
            MetricUnits.Gigabytes,
            usedMegabytes / MegabytesPerGigabyte));
        values.Add(GpuValue(
            MetricIds.ForGpu(deviceId, MetricIds.VramTotalSuffix),
            "VRAM total",
            gpu.Name,
            MetricUnits.Gigabytes,
            totalMegabytes / MegabytesPerGigabyte));
    }

    private void AddNetwork(List<TelemetryValue> values, List<IHardware> networks)
    {
        if (networks.Count == 0)
        {
            AddUnavailableNetwork(values, "No network interfaces found by LibreHardwareMonitor.");
            return;
        }

        double totalSentBytesPerSec = 0;
        double totalReceivedBytesPerSec = 0;

        foreach (IHardware network in networks)
        {
            BufferAllSensors(network);
            double? upload = GetFirstValue(
                _sensorBuffer,
                SensorType.Throughput,
                NetworkUpload);
            if (upload.HasValue)
            {
                totalSentBytesPerSec += upload.Value;
            }

            double? download = GetFirstValue(
                _sensorBuffer,
                SensorType.Throughput,
                NetworkDownload);
            if (download.HasValue)
            {
                totalReceivedBytesPerSec += download.Value;
            }
        }

        values.Add(Value(
            MetricIds.NetworkSend,
            "Send",
            MetricCategories.Network,
            MetricUnits.MegabitsPerSecond,
            totalSentBytesPerSec * BytesToMegabits,
            deviceName: "Network"));
        values.Add(Value(
            MetricIds.NetworkReceive,
            "Receive",
            MetricCategories.Network,
            MetricUnits.MegabitsPerSecond,
            totalReceivedBytesPerSec * BytesToMegabits,
            deviceName: "Network"));
    }

    private void BufferAllSensors(IHardware hardware)
    {
        _sensorBuffer.Clear();
        GatherSensors(hardware);

        void GatherSensors(IHardware current)
        {
            foreach (ISensor sensor in current.Sensors)
            {
                _sensorBuffer.Add(sensor);
            }

            foreach (IHardware child in current.SubHardware)
            {
                GatherSensors(child);
            }
        }
    }

    private static double? GetFirstValue(
        List<ISensor> sensors,
        SensorType type,
        string[] preferredNames)
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
                    return sensor.Value;
                }
            }
        }

        foreach (ISensor sensor in sensors)
        {
            if (sensor.SensorType == type && sensor.Value.HasValue)
            {
                return sensor.Value;
            }
        }

        return null;
    }

    private static string StableDeviceId(string identifier)
    {
        return DeviceIdCache.GetOrAdd(identifier, id =>
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
            return Convert.ToHexString(hash)[..10].ToLowerInvariant();
        });
    }
}
