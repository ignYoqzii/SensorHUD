using System;
using System.IO;
using System.IO.Pipes;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Shared;
using Windows.ApplicationModel;

namespace SensorHUD.Services;

/// <summary>
/// Maintains the frontend's one live connection to the independent collector.
/// The background receive loop owns activation, handshake, reconnection, and
/// framing; pages consume only validated snapshots and never perform IPC.
/// </summary>
internal sealed class CollectorClient
{
    private const int AttemptsBeforeNoData = 8;

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private NamedPipeClientStream? _activePipe;
    private TelemetrySnapshot? _latestSnapshot;

    private CollectorClient()
    {
    }

    /// <summary>
    /// Game Bar hosts the display and settings pages in one frontend process,
    /// so both intentionally share one connection and latest-value cache.
    /// </summary>
    public static CollectorClient Shared { get; } = new();

    /// <summary>
    /// Raised on the pipe reader's background context. UI subscribers must
    /// dispatch control work to their owning XAML thread.
    /// </summary>
    public event Action<TelemetrySnapshot>? SnapshotReceived;

    public TelemetrySnapshot? LatestSnapshot =>
        Volatile.Read(ref _latestSnapshot);

    /// <summary>
    /// Starts the reconnecting receive loop once. Startup is non-blocking from
    /// the page's perspective; snapshots arrive through SnapshotReceived.
    /// </summary>
    public async Task StartAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_runTask is not null)
            {
                return;
            }

            string sessionId = Guid.NewGuid().ToString("N");
            _runCancellation = new CancellationTokenSource();
            _runTask = RunSafelyAsync(sessionId, _runCancellation.Token);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Closes the pipe and waits for the receive loop to finish. The service
    /// observes the disconnect and exits after its short reconnection grace.
    /// </summary>
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
            Interlocked.Exchange(ref _activePipe, null)?.Dispose();

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // The Game Bar process is the user-facing safety boundary. An
            // unforeseen IPC failure becomes no data instead of crashing the
            // widget; reopening it creates a completely fresh client session.
            if (!cancellationToken.IsCancellationRequested)
            {
                PublishStatus(sessionId, CollectorStates.NoData);
            }
        }
    }

    private async Task RunConnectionLoopAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        bool launchAttemptedForOutage = false;
        int failedAttempts = 0;
        bool noDataPublished = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeClientStream? pipe = null;
            bool connected = false;
            try
            {
                pipe = new NamedPipeClientStream(
                    ".",
                    CollectorProtocol.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(
                    (int)CollectorProtocol.PipeConnectionAttemptTimeout.TotalMilliseconds,
                    cancellationToken).ConfigureAwait(false);

                Interlocked.Exchange(ref _activePipe, pipe)?.Dispose();
                await PerformHandshakeAsync(pipe, sessionId, cancellationToken)
                    .ConfigureAwait(false);

                // A completed handshake proves that this outage is over. If a
                // later service failure occurs, one new activation is allowed.
                launchAttemptedForOutage = false;
                failedAttempts = 0;
                noDataPublished = false;
                connected = true;
                await ReceiveSnapshotsAsync(pipe, sessionId, cancellationToken)
                    .ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    PublishStatus(sessionId, CollectorStates.Starting);
                }
            }
            catch (Exception exception) when (
                IsExpectedConnectionFailure(exception))
            {
                failedAttempts++;

                if (connected && !cancellationToken.IsCancellationRequested)
                {
                    // Never leave a stale Running snapshot visible while the
                    // client is reconnecting to a failed service.
                    PublishStatus(sessionId, CollectorStates.Starting);
                }

                if (!cancellationToken.IsCancellationRequested &&
                    !launchAttemptedForOutage)
                {
                    launchAttemptedForOutage = true;
                    await TryLaunchCollectorAsync().ConfigureAwait(false);
                }

                if (!cancellationToken.IsCancellationRequested &&
                    !noDataPublished &&
                    failedAttempts >= AttemptsBeforeNoData)
                {
                    noDataPublished = true;
                    PublishStatus(sessionId, CollectorStates.NoData);
                }
            }
            finally
            {
                _ = Interlocked.CompareExchange(ref _activePipe, null, pipe);

                pipe?.Dispose();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        CollectorProtocol.PipeReconnectDelay,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    private static async Task PerformHandshakeAsync(
        Stream pipe,
        string sessionId,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource handshakeSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeSource.CancelAfter(CollectorProtocol.HandshakeTimeout);

        await PipeMessageSerializer.WriteAsync(
            pipe,
            new CollectorMessage
            {
                Kind = CollectorMessageKind.ClientHello,
                SessionId = sessionId,
            },
            handshakeSource.Token).ConfigureAwait(false);

        CollectorMessage? response = await PipeMessageSerializer.ReadAsync(
            pipe,
            handshakeSource.Token).ConfigureAwait(false);
        if (response is not
            {
                Kind: CollectorMessageKind.ServerHello,
                ProtocolVersion: CollectorProtocol.Version,
            } ||
            !string.Equals(
                response.SessionId,
                sessionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                response?.Error ?? "The collector handshake was rejected.");
        }
    }

    private async Task ReceiveSnapshotsAsync(
        Stream pipe,
        string sessionId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            CollectorMessage? message = await PipeMessageSerializer.ReadAsync(
                pipe,
                cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                return;
            }

            if (message.ProtocolVersion != CollectorProtocol.Version ||
                !string.Equals(
                    message.SessionId,
                    sessionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The collector sent a message for an incompatible session.");
            }

            if (message.Kind == CollectorMessageKind.Error)
            {
                throw new InvalidDataException(
                    message.Error ?? "The collector reported an IPC error.");
            }

            if (message is not
                {
                    Kind: CollectorMessageKind.Snapshot,
                    Snapshot: not null,
                })
            {
                throw new InvalidDataException(
                    "The collector sent an unsupported message.");
            }

            TelemetrySnapshot snapshot = message.Snapshot;
            if (!string.Equals(
                snapshot.SessionId,
                sessionId,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The snapshot belongs to another frontend session.");
            }

            Volatile.Write(ref _latestSnapshot, snapshot);
            NotifySnapshotReceived(snapshot);
        }
    }

    private void NotifySnapshotReceived(TelemetrySnapshot snapshot)
    {
        Delegate[] subscribers =
            SnapshotReceived?.GetInvocationList() ?? Array.Empty<Delegate>();
        foreach (Delegate subscriber in subscribers)
        {
            try
            {
                ((Action<TelemetrySnapshot>)subscriber)(snapshot);
            }
            catch
            {
                // A page failure must not tear down the shared IPC connection.
            }
        }
    }

    private void PublishStatus(string sessionId, string status)
    {
        TelemetrySnapshot snapshot = new()
        {
            SessionId = sessionId,
            CollectorStatus = status,
        };
        Volatile.Write(ref _latestSnapshot, snapshot);
        NotifySnapshotReceived(snapshot);
    }

    private static async Task TryLaunchCollectorAsync()
    {
        try
        {
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync(
                CollectorProtocol.FullTrustGroup);
        }
        catch
        {
            // Missing payload, policy restrictions, and declined elevation all
            // remain normal no-data states. Reopening the widget starts a new
            // frontend session and permits another explicit launch attempt.
        }
    }

    private static bool IsExpectedConnectionFailure(Exception exception)
    {
        return exception is IOException or
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
}
