using System.IO.Pipes;
using System.Threading.Channels;
using SensorHUD.Collector.Ipc;
using SensorHUD.Collector.Sampling;
using SensorHUD.Shared;

namespace SensorHUD.Collector.Hosting;

/// <summary>
/// Owns the service lifetime: accept a UI connection, validate its handshake,
/// sample providers, and publish only the newest snapshot. Sampling and IPC are
/// intentionally separate tasks so a slow UI never creates telemetry backlog.
/// </summary>
internal sealed class CollectorService(
    TelemetryCollector collector,
    TelemetryPipeServer pipeServer)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        TimeSpan connectionWindow = CollectorProtocol.InitialClientTimeout;

        while (!cancellationToken.IsCancellationRequested)
        {
            using NamedPipeServerStream pipe = pipeServer.Create();
            if (!await WaitForConnectionAsync(
                pipe,
                connectionWindow,
                cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (!pipeServer.IsExpectedClient(pipe))
            {
                // The ACL must include World for AppContainer token semantics.
                // Runtime package verification rejects ordinary Win32 clients.
                continue;
            }

            await ServeConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);

            // Game Bar can recreate or briefly suspend a widget. Keep the
            // service warm for a bounded period, then release all providers.
            connectionWindow = CollectorProtocol.ReconnectGracePeriod;
        }
    }

    private static async Task<bool> WaitForConnectionAsync(
        NamedPipeServerStream pipe,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await pipe.WaitForConnectionAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task ServeConnectionAsync(
        NamedPipeServerStream pipe,
        CancellationToken serviceCancellationToken)
    {
        CollectorMessage? hello = await ReadHandshakeAsync(
            pipe,
            serviceCancellationToken).ConfigureAwait(false);
        if (!IsValidHello(hello))
        {
            await TryWriteProtocolErrorAsync(
                pipe,
                hello?.SessionId ?? string.Empty,
                "The collector protocol handshake is invalid.",
                serviceCancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = hello!.SessionId;
        CollectorMessage serverHello = new()
        {
            Kind = CollectorMessageKind.ServerHello,
            SessionId = sessionId,
        };

        try
        {
            await PipeMessageSerializer.WriteAsync(
                pipe,
                serverHello,
                serviceCancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsConnectionException(exception))
        {
            return;
        }

        BoundedChannelOptions channelOptions = new(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        };
        Channel<TelemetrySnapshot> snapshots =
            Channel.CreateBounded<TelemetrySnapshot>(channelOptions);

        using CancellationTokenSource connectionSource =
            CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken);
        Task sampler = SampleAsync(
            snapshots.Writer,
            sessionId,
            connectionSource.Token);
        Task publisher = PublishAsync(
            pipe,
            snapshots.Reader,
            connectionSource.Token);
        Task disconnectMonitor = MonitorDisconnectAsync(
            pipe,
            connectionSource.Token);

        await Task.WhenAny(publisher, disconnectMonitor).ConfigureAwait(false);
        connectionSource.Cancel();
        snapshots.Writer.TryComplete();

        await IgnoreExpectedShutdownAsync(sampler).ConfigureAwait(false);
        await IgnoreExpectedShutdownAsync(publisher).ConfigureAwait(false);
        await IgnoreExpectedShutdownAsync(disconnectMonitor).ConfigureAwait(false);
    }

    private async Task SampleAsync(
        ChannelWriter<TelemetrySnapshot> writer,
        string sessionId,
        CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(CollectorProtocol.SampleInterval);

        try
        {
            do
            {
                TelemetrySample sample = collector.Sample();
                TelemetrySnapshot snapshot = new()
                {
                    SessionId = sessionId,
                    CollectorStatus = CollectorStates.Running,
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                    Diagnostics = sample.Diagnostics,
                    Values = sample.Values,
                };

                // DropOldest means TryWrite normally succeeds and ensures that
                // a blocked pipe can retain at most one obsolete snapshot.
                _ = writer.TryWrite(snapshot);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken)
                .ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static async Task PublishAsync(
        Stream pipe,
        ChannelReader<TelemetrySnapshot> reader,
        CancellationToken cancellationToken)
    {
        await foreach (TelemetrySnapshot snapshot in
            reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await PipeMessageSerializer.WriteAsync(
                pipe,
                new CollectorMessage
                {
                    Kind = CollectorMessageKind.Snapshot,
                    SessionId = snapshot.SessionId,
                    Snapshot = snapshot,
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MonitorDisconnectAsync(
        Stream pipe,
        CancellationToken cancellationToken)
    {
        // The current protocol is server-push after the handshake. Reading is
        // retained solely to detect an orderly client close without waiting for
        // the next sample write.
        CollectorMessage? unexpected = await PipeMessageSerializer.ReadAsync(
            pipe,
            cancellationToken).ConfigureAwait(false);
        if (unexpected is not null)
        {
            throw new InvalidDataException(
                "The client sent an unsupported post-handshake message.");
        }
    }

    private static async Task<CollectorMessage?> ReadHandshakeAsync(
        Stream pipe,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource handshakeSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeSource.CancelAfter(CollectorProtocol.HandshakeTimeout);

        try
        {
            return await PipeMessageSerializer.ReadAsync(
                pipe,
                handshakeSource.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or
            IOException or
            InvalidDataException or
            System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static bool IsValidHello(CollectorMessage? message)
    {
        return message is
        {
            Kind: CollectorMessageKind.ClientHello,
            ProtocolVersion: CollectorProtocol.Version,
            SessionId.Length: > 0 and <= 64,
        };
    }

    private static async Task TryWriteProtocolErrorAsync(
        Stream pipe,
        string sessionId,
        string error,
        CancellationToken cancellationToken)
    {
        try
        {
            await PipeMessageSerializer.WriteAsync(
                pipe,
                new CollectorMessage
                {
                    Kind = CollectorMessageKind.Error,
                    SessionId = sessionId,
                    Error = error,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsConnectionException(exception))
        {
        }
    }

    private static async Task IgnoreExpectedShutdownAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or
            IOException or
            EndOfStreamException or
            InvalidDataException or
            ObjectDisposedException)
        {
        }
    }

    private static bool IsConnectionException(Exception exception)
    {
        return exception is IOException or
            EndOfStreamException or
            InvalidDataException or
            ObjectDisposedException or
            OperationCanceledException;
    }
}
