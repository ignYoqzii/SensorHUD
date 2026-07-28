using System;
using System.Collections.Generic;

namespace SensorHUD.Shared;

/// <summary>
/// One complete, point-in-time result published by the collector host.
/// </summary>
public sealed class TelemetrySnapshot
{
    /// <summary>
    /// Identifies the widget session that requested this data. The widget
    /// ignores snapshots left by an older session.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    public string CollectorStatus { get; set; } = CollectorStates.Starting;

    public List<TelemetryValue> Values { get; set; } = [];
}

/// <summary>
/// One self-describing numeric reading. A null Value means the source knows
/// about the metric but cannot currently provide it; Error explains why.
/// </summary>
public sealed class TelemetryValue
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public double? Value { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string? Error { get; set; }
}
