using System;
using System.Collections.Generic;

namespace SensorHUD.Core.Telemetry;

/// <summary>
/// One point-in-time collector result. Session identity belongs exclusively
/// to the transport envelope and is deliberately absent from this model.
/// </summary>
public sealed class TelemetrySnapshot
{
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public CollectorHealth Health { get; set; } = new();

    public List<MetricReading> Readings { get; set; } = [];
}
