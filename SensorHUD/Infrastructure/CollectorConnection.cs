using System;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Core.Telemetry;
using SensorHUD.Core.Transport;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Frontend-visible collector connection states.
/// </summary>
internal enum CollectorConnectionState
{
    Stopped,
    Connecting,
    Connected,
    Unavailable,
}

/// <summary>
/// Immutable status raised independently from telemetry snapshots.
/// </summary>
internal sealed record CollectorConnectionStatus(
    CollectorConnectionState State,
    string? Error = null);

/// <summary>
/// Owns idempotent collector lifecycle, reconnection policy, and the latest
/// validated telemetry snapshot. It contains no XAML or pipe framing logic.
/// </summary>
internal sealed class CollectorConnection
{
    private readonly CollectorLauncher _launcher;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private CollectorPipeClient? _activeClient;
    private TelemetrySnapshot? _latestSnapshot;
    private CollectorConnectionStatus _status =
        new(CollectorConnectionState.Stopped);

    public CollectorConnection(CollectorLauncher launcher)
    {
        _launcher = launcher;
    }

    /// <summary>
    /// Raised on a background context. XAML subscribers must dispatch.
    /// </summary>
    public event Action<TelemetrySnapshot>? SnapshotReceived;

    /// <summary>
    /// Raised on a background context when frontend connectivity changes.
    /// </summary>
    public event Action<CollectorConnectionStatus>? StatusChanged;

    public TelemetrySnapshot? LatestSnapshot =>
        Volatile.Read(ref _latestSnapshot);

    public CollectorConnectionStatus Status =>
        Volatile.Read(ref _status);

    public async Task StartAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_runTask is not null)
            {
                return;
            }

            _runCancellation = new CancellationTokenSource();
            string sessionId = Guid.NewGuid().ToString("N");
            _runTask = RunSafelyAsync(sessionId, _runCancellation.Token);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_runTask is null || _runCancellation is null)
            {
                return;
            }

            _runCancellation.Cancel();
            Interlocked.Exchange(ref _activeClient, null)?.Dispose();
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
            }

            _runCancellation.Dispose();
            _runCancellation = null;
            _runTask = null;
            Volatile.Write(ref _latestSnapshot, null);
            PublishStatus(CollectorConnectionState.Stopped);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task RunSafelyAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunConnectionLoopAsync(sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                PublishStatus(
                    CollectorConnectionState.Unavailable,
                    exception.Message);
            }
        }
    }

    private async Task RunConnectionLoopAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        bool launchAttempted = false;
        int failedAttempts = 0;
        PublishStatus(CollectorConnectionState.Connecting);

        while (!cancellationToken.IsCancellationRequested)
        {
            CollectorPipeClient? client = null;
            try
            {
                client = new CollectorPipeClient();
                Interlocked.Exchange(ref _activeClient, client)?.Dispose();
                await client.ConnectAsync(sessionId, cancellationToken)
                    .ConfigureAwait(false);

                launchAttempted = false;
                failedAttempts = 0;
                PublishStatus(CollectorConnectionState.Connected);
                await ReceiveAsync(client, sessionId, cancellationToken)
                    .ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    PublishStatus(CollectorConnectionState.Connecting);
                }
            }
            catch (Exception exception)
                when (IsExpectedConnectionFailure(exception))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                failedAttempts++;
                PublishStatus(
                    failedAttempts >= CollectorProtocol.AttemptsBeforeUnavailable
                        ? CollectorConnectionState.Unavailable
                        : CollectorConnectionState.Connecting,
                    exception.Message);

                if (!launchAttempted)
                {
                    launchAttempted = true;
                    await _launcher.TryLaunchAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _ = Interlocked.CompareExchange(
                    ref _activeClient,
                    null,
                    client);
                client?.Dispose();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(
                    CollectorProtocol.ReconnectDelay,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReceiveAsync(
        CollectorPipeClient client,
        string sessionId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TelemetrySnapshot? snapshot = await client.ReadSnapshotAsync(
                sessionId,
                cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return;
            }

            Volatile.Write(ref _latestSnapshot, snapshot);
            Notify(SnapshotReceived, snapshot);
        }
    }

    private void PublishStatus(
        CollectorConnectionState state,
        string? error = null)
    {
        CollectorConnectionStatus status = new(state, error);
        Volatile.Write(ref _status, status);
        Notify(StatusChanged, status);
    }

    private static void Notify<T>(Action<T>? eventHandler, T value)
    {
        foreach (Delegate subscriber in
                 eventHandler?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action<T>)subscriber)(value);
            }
            catch
            {
                // One page must never tear down the process-wide connection.
            }
        }
    }

    private static bool IsExpectedConnectionFailure(Exception exception) =>
        exception is IOException or
            UnauthorizedAccessException or
            TimeoutException or
            InvalidDataException or
            EndOfStreamException or
            ObjectDisposedException or
            JsonException or
            SecurityException or
            NotSupportedException or
            OperationCanceledException;
}
