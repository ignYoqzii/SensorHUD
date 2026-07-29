using SensorHUD.Core.Telemetry;

namespace SensorHUD.Core.Transport;

/// <summary>
/// Identifies the small set of operations allowed across the privilege
/// boundary.
/// </summary>
public enum CollectorMessageKind
{
    ClientHello = 1,
    ServerHello = 2,
    Snapshot = 3,
    Error = 4,
}

/// <summary>
/// Versioned envelope for each length-prefixed pipe frame.
/// </summary>
public sealed class CollectorMessage
{
    public int ProtocolVersion { get; set; } = CollectorProtocol.Version;

    public CollectorMessageKind Kind { get; set; }

    /// <summary>
    /// Random identity for one frontend lifetime, used to reject stale peers.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    public TelemetrySnapshot? Snapshot { get; set; }

    public string? Error { get; set; }
}
