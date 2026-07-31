using System;
using System.Linq;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Read-only collector status presented by the settings widget.
/// </summary>
public sealed class CollectorStatusViewModel : ObservableObject
{
    private string _connection = "Stopped";
    private string _lastSnapshot = "Waiting";
    private string _administrator = "Unknown";
    private string _pawnIo = "Unknown";
    private string _frameCapture = "Inactive";
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

    public string FrameCapture
    {
        get => _frameCapture;
        private set => SetProperty(ref _frameCapture, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                Notify(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

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
        Error = FormatErrors(connection.Error, snapshot?.Health);

        if (snapshot is null)
        {
            return;
        }

        LastSnapshot = snapshot.CapturedAtUtc
            .ToLocalTime()
            .ToString("T");
        Administrator = snapshot.Health.IsAdministrator ? "Yes" : "No";
        PawnIo = FormatPawnIo(snapshot.Health);
        FrameCapture = snapshot.Health.IsFrameCaptureActive
            ? "Active"
            : "Inactive";
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

    private static string? FormatErrors(
        string? connectionError,
        CollectorHealth? health)
    {
        if (!string.IsNullOrWhiteSpace(connectionError))
        {
            return connectionError;
        }

        string[] errors =
        [
            .. new[] { health?.PawnIoError, health?.FrameCaptureError }
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => error!),
        ];
        return errors.Length == 0
            ? null
            : string.Join(Environment.NewLine, errors);
    }
}
