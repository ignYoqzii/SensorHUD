using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Core.Telemetry;
using SensorHUD.Core.Transport;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Owns exactly one named-pipe connection, including handshake and validated
/// message reads. Reconnection policy belongs to <see cref="CollectorConnection"/>.
/// </summary>
internal sealed partial class CollectorPipeClient : IDisposable
{
    private readonly NamedPipeClientStream _pipe = new(
        ".",
        CollectorProtocol.PipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);

    public async Task ConnectAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        await _pipe.ConnectAsync(
            (int)CollectorProtocol.ConnectionAttemptTimeout.TotalMilliseconds,
            cancellationToken).ConfigureAwait(false);

        using CancellationTokenSource handshake =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshake.CancelAfter(CollectorProtocol.HandshakeTimeout);

        await PipeMessageSerializer.WriteAsync(
            _pipe,
            new CollectorMessage
            {
                Kind = CollectorMessageKind.ClientHello,
                SessionId = sessionId,
            },
            handshake.Token).ConfigureAwait(false);

        CollectorMessage? response = await PipeMessageSerializer.ReadAsync(
            _pipe,
            handshake.Token).ConfigureAwait(false);
        ValidateEnvelope(response, CollectorMessageKind.ServerHello, sessionId);
    }

    public async Task<TelemetrySnapshot?> ReadSnapshotAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        CollectorMessage? message = await PipeMessageSerializer.ReadAsync(
            _pipe,
            cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return null;
        }

        ValidateEnvelope(message, CollectorMessageKind.Snapshot, sessionId);
        return message.Snapshot ??
            throw new InvalidDataException(
                "The collector sent an empty telemetry message.");
    }

    public void Dispose() => _pipe.Dispose();

    private static void ValidateEnvelope(
        CollectorMessage? message,
        CollectorMessageKind expectedKind,
        string sessionId)
    {
        if (message is null)
        {
            throw new EndOfStreamException(
                "The collector closed the connection.");
        }

        if (message.ProtocolVersion != CollectorProtocol.Version)
        {
            throw new InvalidDataException(
                $"Collector protocol {message.ProtocolVersion} is not supported.");
        }

        if (!string.Equals(
                message.SessionId,
                sessionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The collector message belongs to another frontend session.");
        }

        if (message.Kind == CollectorMessageKind.Error)
        {
            throw new InvalidDataException(
                message.Error ?? "The collector rejected the request.");
        }

        if (message.Kind != expectedKind)
        {
            throw new InvalidDataException(
                $"Expected '{expectedKind}', received '{message.Kind}'.");
        }
    }
}
