namespace SensorHUD.Core.Telemetry;

/// <summary>
/// Current state of the optional PawnIO hardware-access dependency.
/// </summary>
public enum PawnIoState
{
    Unavailable,
    Ready,
    RestartRequired,
}

/// <summary>
/// Small, non-sensitive health summary sent with telemetry samples.
/// </summary>
public sealed class CollectorHealth
{
    public bool IsAdministrator { get; set; }

    public PawnIoState PawnIoState { get; set; }

    public string? PawnIoVersion { get; set; }

    public string? PawnIoError { get; set; }

    /// <summary>
    /// Gets or sets whether the collector's frame-capture subsystem is
    /// actively consuming presentation events.
    /// </summary>
    public bool IsFrameCaptureActive { get; set; }

    /// <summary>
    /// Gets or sets a coarse frame-capture subsystem failure, if any.
    /// Metric availability remains represented only by reading absence.
    /// </summary>
    public string? FrameCaptureError { get; set; }
}
