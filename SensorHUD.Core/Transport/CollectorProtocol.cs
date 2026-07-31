using System;

namespace SensorHUD.Core.Transport;

/// <summary>
/// Shared protocol identifiers, timing policy, and safety limits. Changing the
/// wire contract requires incrementing <see cref="Version"/>.
/// </summary>
public static class CollectorProtocol
{
    public const int Version = 2;
    public const string FullTrustGroup = "Collector";
    public const string SemaphoreName = @"Local\SensorHUD.Collector";
    public const string PipeName = @"LOCAL\SensorHUD.Telemetry.v2";
    public const int MaximumMessageBytes = 1024 * 1024;
    public const int AttemptsBeforeUnavailable = 8;

    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan ConnectionAttemptTimeout =
        TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan ReconnectDelay =
        TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan InitialClientTimeout =
        TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ReconnectGracePeriod =
        TimeSpan.FromSeconds(8);
    public static readonly TimeSpan HandshakeTimeout =
        TimeSpan.FromSeconds(3);
}
