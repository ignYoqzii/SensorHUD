using System;

namespace SensorHUD.Shared;

/// <summary>
/// Stable metric identifiers shared by data producers and the display catalog.
/// IDs are persisted in user settings and therefore must remain unique.
/// </summary>
public static class MetricIds
{
    public const string Fps = "fps";
    public const string OnePercentLow = "fps.onePercentLow";
    public const string Frametime = "fps.frametime";
    public const string CpuUsage = "cpu.usage";
    public const string CpuTemperature = "cpu.temperature";
    public const string RamUsage = "ram.usage";
    public const string NetworkSend = "network.send";
    public const string NetworkReceive = "network.receive";

    public const string GpuPrefix = "gpu.";
    public const string UsageSuffix = ".usage";
    public const string TemperatureSuffix = ".temperature";
    public const string VramSuffix = ".vram";

    public static string ForGpu(string deviceId, string suffix) =>
        $"{GpuPrefix}{deviceId}{suffix}";
}

public static class MetricCategories
{
    public const string FrameRate = "FPS";
    public const string Cpu = "CPU";
    public const string Gpu = "GPU";
    public const string Ram = "RAM";
    public const string Network = "Network";
}

public static class MetricUnits
{
    public const string FramesPerSecond = "FPS";
    public const string Milliseconds = "ms";
    public const string Percent = "%";
    public const string Celsius = "°C";
    public const string MegabitsPerSecond = "Mbps";
}

public static class CollectorStates
{
    public const string Starting = "Starting";
    public const string Running = "Running";
    public const string NoData = "No telemetry data to display";
}

/// <summary>
/// Activation identifiers and timing values shared by the frontend and
/// collector. Keeping them with the wire contracts prevents protocol drift.
/// </summary>
public static class CollectorProtocol
{
    public const int Version = 1;
    public const string FullTrustGroup = "Collector";
    public const string SemaphoreName = @"Local\SensorHUD.Collector";
    public const string PipeName = @"LOCAL\SensorHUD.Telemetry.v1";
    public const string SettingsFile = "settings.json";
    public const int MaximumMessageBytes = 1024 * 1024;

    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan PipeConnectionAttemptTimeout =
        TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan PipeReconnectDelay =
        TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan InitialClientTimeout =
        TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ReconnectGracePeriod =
        TimeSpan.FromSeconds(8);
    public static readonly TimeSpan HandshakeTimeout =
        TimeSpan.FromSeconds(3);
    public static readonly TimeSpan SettingsSaveDelay = TimeSpan.FromMilliseconds(350);
}
