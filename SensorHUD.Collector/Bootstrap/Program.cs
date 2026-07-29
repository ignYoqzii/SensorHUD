using System.Security.Principal;
using SensorHUD.Collector.Hosting;
using SensorHUD.Collector.Sampling;
using SensorHUD.Collector.Transport;
using SensorHUD.Core.Transport;
using Windows.ApplicationModel;

namespace SensorHUD.Collector.Bootstrap;

/// <summary>
/// Composition root for the windowless collector process.
/// </summary>
internal static class Program
{
    private static async Task Main()
    {
        // Only one service may sample hardware and own the pipe endpoint.
        using Semaphore singleton = new(
            initialCount: 1,
            maximumCount: 1,
            name: CollectorProtocol.SemaphoreName,
            createdNew: out _);

        // Concurrent widget activations may request the same full-trust
        // process. The first collector owns sampling and IPC; duplicates exit
        // immediately instead of waiting or creating redundant providers.
        bool ownsSingleton = singleton.WaitOne(0);
        if (!ownsSingleton)
        {
            return;
        }

        try
        {
            string packageFamilyName;
            try
            {
                packageFamilyName = Package.Current.Id.FamilyName;
            }
            catch
            {
                // The collector is intentionally package-only. Its package
                // identity anchors pipe namespace resolution and client
                // authorization, so an unpackaged launch is never accepted.
                return;
            }

            // The executable manifest requires administrator access, so
            // Windows displays UAC before Main runs. Refuse a non-elevated
            // launch rather than operating in an untested partial mode.
            if (!IsRunningAsAdministrator())
            {
                return;
            }

            // This check runs on every collector start, but the bundled
            // installer runs only when PawnIO is missing, damaged, or older
            // than the required version.
            PawnIoDependency.PawnIoResult pawnIo =
                await PawnIoDependency.EnsureReadyAsync()
                    .ConfigureAwait(false);
            using TelemetrySampler sampler =
                TelemetrySampler.CreateDefault(pawnIo);
            SecurePipeServer pipeServer = new(packageFamilyName);
            CollectorHost host = new(sampler, pipeServer);
            await host.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            if (ownsSingleton)
            {
                singleton.Release();
            }
        }
    }

    private static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity)
            .IsInRole(WindowsBuiltInRole.Administrator);
    }
}
