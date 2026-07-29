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
/// Current state of ETW frame capture.
/// </summary>
public enum FrameCaptureState
{
    Starting,
    WaitingForProcess,
    WarmingUp,
    Active,
    Unavailable,
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

    public FrameCaptureState FrameCaptureState { get; set; }

    /// <summary>
    /// Process selected from recent presentation activity for frame
    /// telemetry.
    /// </summary>
    public string? ForegroundProcess { get; set; }

    public string? FrameCaptureError { get; set; }

    /// <summary>
    /// The most recent unexpected provider failure. Individual unavailable
    /// sensors continue to report their own errors on their readings.
    /// </summary>
    public string? LastProviderError { get; set; }
}
