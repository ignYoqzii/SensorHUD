using System;
using System.Threading.Tasks;
using SensorHUD.Core.Transport;
using Windows.ApplicationModel;

namespace SensorHUD.Infrastructure;

/// <summary>
/// Requests activation of the packaged elevated collector.
/// </summary>
internal sealed class CollectorLauncher
{
    /// <summary>
    /// Returns false for expected activation failures such as declined UAC,
    /// missing package payload, or system policy restrictions.
    /// </summary>
    public async Task<bool> TryLaunchAsync()
    {
        try
        {
            await FullTrustProcessLauncher
                .LaunchFullTrustProcessForCurrentAppAsync(
                    CollectorProtocol.FullTrustGroup);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
