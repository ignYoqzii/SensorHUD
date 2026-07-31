using System;
using System.Collections.Generic;

namespace SensorHUD.Core.Telemetry;

/// <summary>
/// One point-in-time collector result. Session identity belongs exclusively
/// to the transport envelope and is deliberately absent from this model.
/// </summary>
public sealed class TelemetrySnapshot
{
    public required DateTimeOffset CapturedAtUtc { get; set; }

    public required CollectorHealth Health { get; set; }

    public required List<MetricInstance> Instances { get; set; }

    public required List<MetricReading> Readings { get; set; }
}
