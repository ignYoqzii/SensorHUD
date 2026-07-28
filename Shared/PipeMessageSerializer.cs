using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SensorHUD.Shared;

/// <summary>
/// Reads and writes length-prefixed JSON frames. Pipes are byte streams, so a
/// single write is not guaranteed to match a single read; the prefix preserves
/// message boundaries without relying on pipe transmission mode.
/// </summary>
public static class PipeMessageSerializer
{
    private const int HeaderLength = sizeof(int);

    public static async ValueTask WriteAsync(
        Stream stream,
        CollectorMessage message,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            message,
            TelemetryJsonContext.Default.CollectorMessage);
        if (payload.Length > CollectorProtocol.MaximumMessageBytes)
        {
            throw new InvalidDataException(
                $"Collector message is {payload.Length} bytes; the maximum is " +
                $"{CollectorProtocol.MaximumMessageBytes}.");
        }

        byte[] header = new byte[HeaderLength];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<CollectorMessage?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderLength];
        if (!await TryReadExactlyAsync(stream, header, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength <= 0 ||
            payloadLength > CollectorProtocol.MaximumMessageBytes)
        {
            throw new InvalidDataException(
                $"Invalid collector message length: {payloadLength}.");
        }

        byte[] payload = GC.AllocateUninitializedArray<byte>(payloadLength);
        if (!await TryReadExactlyAsync(stream, payload, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new EndOfStreamException(
                "The collector pipe closed in the middle of a message.");
        }

        return JsonSerializer.Deserialize(
            payload,
            TelemetryJsonContext.Default.CollectorMessage);
    }

    private static async ValueTask<bool> TryReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < destination.Length)
        {
            int read = await stream.ReadAsync(
                destination[totalRead..],
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (totalRead == 0)
                {
                    return false;
                }

                throw new EndOfStreamException(
                    "The collector pipe closed in the middle of a message.");
            }

            totalRead += read;
        }

        return true;
    }
}
