using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SensorHUD.Core.Transport;

/// <summary>
/// Reads and writes size-limited, length-prefixed JSON pipe frames.
/// </summary>
public static class PipeMessageSerializer
{
    private const int HeaderLength = sizeof(int);

    /// <summary>
    /// Writes one validated protocol envelope to a stream.
    /// </summary>
    public static async ValueTask WriteAsync(
        Stream stream,
        CollectorMessage message,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            message,
            CollectorJsonContext.Default.CollectorMessage);
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

    /// <summary>
    /// Reads one envelope, or null when the peer closes between frames.
    /// </summary>
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
            CollectorJsonContext.Default.CollectorMessage);
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
                return totalRead == 0
                    ? false
                    : throw new EndOfStreamException(
                        "The collector pipe closed in the middle of a message.");
            }

            totalRead += read;
        }

        return true;
    }
}
