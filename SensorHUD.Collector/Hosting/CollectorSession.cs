using System.IO.Pipes;
using System.Threading.Channels;
using SensorHUD.Collector.Sampling;
using SensorHUD.Core.Telemetry;
using SensorHUD.Core.Transport;

namespace SensorHUD.Collector.Hosting;

/// <summary>
/// Validates and serves one frontend connection. Sampling and publishing use a
/// capacity-one drop-oldest channel so a slow widget cannot create backlog.
/// </summary>
internal sealed class CollectorSession(
    TelemetrySampler sampler,
    NamedPipeServerStream pipe)
{
    public async Task RunAsync(CancellationToken hostCancellation)
    {
        CollectorMessage? hello = await ReadHandshakeAsync(
            hostCancellation).ConfigureAwait(false);
        if (!IsValidHello(hello))
        {
            await TryWriteProtocolErrorAsync(
                hello?.SessionId ?? string.Empty,
                "The collector protocol handshake is invalid.",
                hostCancellation).ConfigureAwait(false);
            return;
        }

        string sessionId = hello!.SessionId;
        try
        {
            await PipeMessageSerializer.WriteAsync(
                pipe,
                new CollectorMessage
                {
                    Kind = CollectorMessageKind.ServerHello,
                    SessionId = sessionId,
                },
                hostCancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsConnectionException(exception))
        {
            return;
        }

        Channel<TelemetrySnapshot> snapshots =
            Channel.CreateBounded<TelemetrySnapshot>(
                new BoundedChannelOptions(1)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true,
                });
        using CancellationTokenSource sessionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                hostCancellation);

        Task sampler = SampleAsync(
            snapshots.Writer,
            sessionCancellation.Token);
        Task publisher = PublishAsync(
            snapshots.Reader,
            sessionId,
            sessionCancellation.Token);
        Task disconnect = MonitorDisconnectAsync(
            sessionCancellation.Token);

        await Task.WhenAny(publisher, disconnect).ConfigureAwait(false);
        await sessionCancellation.CancelAsync().ConfigureAwait(false);
        snapshots.Writer.TryComplete();
        await IgnoreExpectedShutdownAsync(sampler).ConfigureAwait(false);
        await IgnoreExpectedShutdownAsync(publisher).ConfigureAwait(false);
        await IgnoreExpectedShutdownAsync(disconnect).ConfigureAwait(false);
    }

    private async Task SampleAsync(
        ChannelWriter<TelemetrySnapshot> writer,
        CancellationToken cancellationToken)
    {
        using PeriodicTimer timer =
            new(CollectorProtocol.SampleInterval);
        try
        {
            do
            {
                _ = writer.TryWrite(sampler.Sample());
            }
            while (await timer.WaitForNextTickAsync(cancellationToken)
                .ConfigureAwait(false));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task PublishAsync(
        ChannelReader<TelemetrySnapshot> reader,
        string sessionId,
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
                    SessionId = sessionId,
                    Snapshot = snapshot,
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MonitorDisconnectAsync(
        CancellationToken cancellationToken)
    {
        CollectorMessage? unexpected =
            await PipeMessageSerializer.ReadAsync(
                pipe,
                cancellationToken).ConfigureAwait(false);
        if (unexpected is not null)
        {
            throw new InvalidDataException(
                "The client sent an unsupported post-handshake message.");
        }
    }

    private async Task<CollectorMessage?> ReadHandshakeAsync(
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource handshake =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        handshake.CancelAfter(CollectorProtocol.HandshakeTimeout);
        try
        {
            return await PipeMessageSerializer.ReadAsync(
                pipe,
                handshake.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is OperationCanceledException or
                IOException or
                InvalidDataException or
                System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task TryWriteProtocolErrorAsync(
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

    private static bool IsValidHello(CollectorMessage? message) =>
        message is
        {
            Kind: CollectorMessageKind.ClientHello,
            ProtocolVersion: CollectorProtocol.Version,
            SessionId.Length: > 0 and <= 64,
        };

    private static async Task IgnoreExpectedShutdownAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
            when (IsConnectionException(exception))
        {
        }
    }

    private static bool IsConnectionException(Exception exception) =>
        exception is IOException or
            EndOfStreamException or
            InvalidDataException or
            ObjectDisposedException or
            OperationCanceledException;
}
