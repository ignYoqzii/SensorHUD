namespace SensorHUD.Core.Telemetry;

/// <summary>
/// Declares one detected device instance for a per-device registry metric.
/// Instance discovery is independent of whether that metric currently has a
/// numeric reading.
/// </summary>
public sealed class MetricInstance
{
    public required string MetricId { get; set; }

    public required string DeviceId { get; set; }

    public string? DeviceName { get; set; }
}
