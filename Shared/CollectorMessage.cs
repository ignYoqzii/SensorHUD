namespace SensorHUD.Shared;

/// <summary>
/// Identifies the small set of operations permitted across the collector's
/// privilege boundary. Unknown values are rejected during the handshake.
/// </summary>
public enum CollectorMessageKind
{
    ClientHello = 1,
    ServerHello = 2,
    Snapshot = 3,
    Error = 4,
}

/// <summary>
/// Versioned envelope used for every pipe frame. Keeping one explicit envelope
/// makes compatibility checks and message-size validation independent from
/// individual telemetry models.
/// </summary>
public sealed class CollectorMessage
{
    public int ProtocolVersion { get; set; } = CollectorProtocol.Version;

    public CollectorMessageKind Kind { get; set; }

    /// <summary>
    /// Random identifier created for one frontend lifetime. Both processes use
    /// it to reject stale messages after a reconnection.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    public TelemetrySnapshot? Snapshot { get; set; }

    public string? Error { get; set; }
}
