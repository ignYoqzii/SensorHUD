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
    /// ignores snapshots belonging to a different live session.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    public string CollectorStatus { get; set; } = CollectorStates.Starting;

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public CollectorDiagnostics Diagnostics { get; set; } = new();

    public List<TelemetryValue> Values { get; set; } = [];
}

/// <summary>
/// Small, user-facing health summary produced alongside every snapshot. It
/// intentionally contains no logs or privileged implementation details.
/// </summary>
public sealed class CollectorDiagnostics
{
    public bool IsAdministrator { get; set; }

    public string PawnIoStatus { get; set; } = "Unknown";

    public string FrameMetricsStatus { get; set; } = "Starting";

    public string CpuName { get; set; } = "Not detected";

    public List<string> GpuNames { get; set; } = [];

    /// <summary>
    /// Latest frontend/collector connection failure, when one is available.
    /// Normal unavailable sensors remain attached to their individual values.
    /// </summary>
    public string? LastError { get; set; }
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
