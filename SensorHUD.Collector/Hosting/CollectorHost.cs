using System.IO.Pipes;
using SensorHUD.Collector.Sampling;
using SensorHUD.Collector.Transport;
using SensorHUD.Core.Transport;

namespace SensorHUD.Collector.Hosting;

/// <summary>
/// Owns the collector process lifetime and accepts validated frontend
/// connections. Per-connection work belongs to <see cref="CollectorSession"/>.
/// </summary>
internal sealed class CollectorHost(
    TelemetrySampler sampler,
    SecurePipeServer pipeServer)
{
    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        TimeSpan connectionWindow =
            CollectorProtocol.InitialClientTimeout;
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
                // The World ACL is required by AppContainer token semantics;
                // runtime package validation rejects ordinary Win32 clients.
                continue;
            }

            CollectorSession session = new(sampler, pipe);
            await session.RunAsync(cancellationToken).ConfigureAwait(false);

            // Brief Game Bar suspension or recreation may reconnect. When no
            // frontend returns, the process releases providers and exits.
            connectionWindow = CollectorProtocol.ReconnectGracePeriod;
        }
    }

    private static async Task<bool> WaitForConnectionAsync(
        NamedPipeServerStream pipe,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await pipe.WaitForConnectionAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
