using System;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;
using Windows.UI.Xaml;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Read-only collector status presented by the settings widget.
/// </summary>
public sealed partial class CollectorStatusViewModel : ObservableObject
{
    private string _connection = "Stopped";
    private string _lastSnapshot = "Waiting";
    private string _administrator = "Unknown";
    private string _pawnIo = "Unknown";
    private string _foregroundProcess = "Waiting";
    private string? _error;

    public string Connection
    {
        get => _connection;
        private set => SetProperty(ref _connection, value);
    }

    public string LastSnapshot
    {
        get => _lastSnapshot;
        private set => SetProperty(ref _lastSnapshot, value);
    }

    public string Administrator
    {
        get => _administrator;
        private set => SetProperty(ref _administrator, value);
    }

    public string PawnIo
    {
        get => _pawnIo;
        private set => SetProperty(ref _pawnIo, value);
    }

    /// <summary>
    /// Gets the process selected from current presentation activity.
    /// </summary>
    public string ForegroundProcess
    {
        get => _foregroundProcess;
        private set => SetProperty(ref _foregroundProcess, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                Notify(nameof(ErrorVisibility));
            }
        }
    }

    public Visibility ErrorVisibility =>
        string.IsNullOrWhiteSpace(Error)
            ? Visibility.Collapsed
            : Visibility.Visible;

    internal void Update(
        CollectorConnectionStatus connection,
        TelemetrySnapshot? snapshot)
    {
        Connection = connection.State switch
        {
            CollectorConnectionState.Stopped => "Stopped",
            CollectorConnectionState.Connecting => "Connecting",
            CollectorConnectionState.Connected => "Connected",
            _ => "Unavailable",
        };
        Error = connection.Error ?? snapshot?.Health.LastProviderError;

        if (snapshot is null)
        {
            return;
        }

        LastSnapshot = snapshot.CapturedAtUtc
            .ToLocalTime()
            .ToString("T");
        Administrator = snapshot.Health.IsAdministrator ? "Yes" : "No";
        PawnIo = FormatPawnIo(snapshot.Health);
        ForegroundProcess = FormatForegroundProcess(snapshot.Health);
    }

    private static string FormatPawnIo(CollectorHealth health)
    {
        string state = health.PawnIoState switch
        {
            PawnIoState.Ready => "Ready",
            PawnIoState.RestartRequired => "Restart required",
            _ => "Unavailable",
        };
        return string.IsNullOrWhiteSpace(health.PawnIoVersion)
            ? state
            : $"{state} ({health.PawnIoVersion})";
    }

    private static string FormatForegroundProcess(CollectorHealth health) =>
        health.FrameCaptureState switch
        {
            FrameCaptureState.WaitingForProcess =>
                "Waiting for a presenting process",
            FrameCaptureState.WarmingUp => "Warming up",
            FrameCaptureState.Active when
                !string.IsNullOrWhiteSpace(health.ForegroundProcess) =>
                health.ForegroundProcess,
            FrameCaptureState.Active => "Active",
            FrameCaptureState.Unavailable => "Unavailable",
            _ => "Starting",
        };
}
