namespace SensorHUD.Core.Telemetry;

/// <summary>
/// One numeric reading published by a collector provider. Presentation
/// metadata belongs to the metric registry rather than the wire payload.
/// </summary>
public sealed class MetricReading
{
    public string MetricId { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public string? DeviceName { get; set; }

    public double? Value { get; set; }

    public string? Error { get; set; }
}
