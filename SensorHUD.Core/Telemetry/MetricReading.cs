namespace SensorHUD.Core.Telemetry;

/// <summary>
/// One available numeric reading published by the collector. Presentation
/// metadata belongs to the metric registry rather than the wire payload.
/// </summary>
public sealed class MetricReading
{
    public required string MetricId { get; set; }

    public string? DeviceId { get; set; }

    public string? DeviceName { get; set; }

    public required double Value { get; set; }
}
