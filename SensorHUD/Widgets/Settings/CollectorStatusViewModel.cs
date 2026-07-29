using System;
using System.Linq;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;
using Windows.UI.Xaml;

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
    private string _frames = "Waiting";
    private string _processor = "Not detected";
    private string _graphics = "Not detected";
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

    public string Frames
    {
        get => _frames;
        private set => SetProperty(ref _frames, value);
    }

    public string Processor
    {
        get => _processor;
        private set => SetProperty(ref _processor, value);
    }

    public string Graphics
    {
        get => _graphics;
        private set => SetProperty(ref _graphics, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                Notify(nameof(HasError));
                Notify(nameof(ErrorVisibility));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public Visibility ErrorVisibility =>
        HasError ? Visibility.Visible : Visibility.Collapsed;

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
        Frames = FormatFrames(snapshot.Health);
        Processor = snapshot.Readings
            .FirstOrDefault(reading =>
                reading.MetricId == MetricRegistry.CpuUsage)
            ?.DeviceName ?? "Not detected";
        string[] gpuNames = snapshot.Readings
            .Where(reading =>
                reading.MetricId == MetricRegistry.GpuUsage &&
                !string.IsNullOrWhiteSpace(reading.DeviceName))
            .Select(reading => reading.DeviceName!)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        Graphics = gpuNames.Length == 0
            ? "Not detected"
            : string.Join(", ", gpuNames);
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

    private static string FormatFrames(CollectorHealth health) =>
        health.FrameCaptureState switch
        {
            FrameCaptureState.WaitingForGame => "Waiting for a game",
            FrameCaptureState.WarmingUp => "Warming up",
            FrameCaptureState.Active when
                !string.IsNullOrWhiteSpace(health.FrameTargetProcess) =>
                $"Active - {health.FrameTargetProcess}",
            FrameCaptureState.Active => "Active",
            FrameCaptureState.Unavailable => "Unavailable",
            _ => "Starting",
        };
}
