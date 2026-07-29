using System.Diagnostics;
using Microsoft.Win32;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Collector.Bootstrap;

/// <summary>
/// Installs the official signed PawnIO dependency on the first elevated run.
/// PawnIO is machine-wide and may be shared by other hardware-monitoring apps,
/// so SensorHUD deliberately never removes an existing installation.
/// </summary>
internal static class PawnIoDependency
{
    private const string InstallerFileName = "PawnIO_setup.exe";
    private const string UninstallRegistryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";
    private const string DriverRegistryPath =
        @"SYSTEM\CurrentControlSet\Services\PawnIO";
    private static readonly Version RequiredVersion = new(2, 2, 0);

    public static async Task<PawnIoResult> EnsureReadyAsync()
    {
        InstallationInfo installation = ReadInstallation();
        if (installation.IsUsable)
        {
            return PawnIoResult.Ready(installation.Version);
        }

        string installer = Path.Combine(
            AppContext.BaseDirectory,
            InstallerFileName);
        if (!File.Exists(installer))
        {
            return PawnIoResult.Failed(
                $"Bundled {InstallerFileName} is missing.");
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = installer,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            startInfo.ArgumentList.Add("-install");
            startInfo.ArgumentList.Add("-silent");

            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException(
                    "PawnIO installer did not start.");

            await process.WaitForExitAsync();
            if (process.ExitCode is not 0 and not 3010)
            {
                return PawnIoResult.Failed(
                    $"PawnIO installation failed with exit code {process.ExitCode}.");
            }

            InstallationInfo verified = ReadInstallation();
            if (!verified.IsUsable)
            {
                return PawnIoResult.Failed(
                    "PawnIO installation completed, but its required driver registration or version could not be verified.");
            }

            return process.ExitCode == 3010
                ? PawnIoResult.RestartRequired(verified.Version)
                : PawnIoResult.Ready(verified.Version);
        }
        catch (Exception exception)
        {
            return PawnIoResult.Failed(
                $"PawnIO installation failed: {exception.Message}");
        }
    }

    internal sealed record PawnIoResult(
        PawnIoState State,
        string? Version,
        string? Error)
    {
        public static PawnIoResult Ready(Version? version) =>
            new(PawnIoState.Ready, version?.ToString(), null);

        public static PawnIoResult RestartRequired(Version? version)
        {
            const string message =
                "PawnIO was installed, but Windows must be restarted before protected hardware sensors become available.";
            return new(
                PawnIoState.RestartRequired,
                version?.ToString(),
                message);
        }

        public static PawnIoResult Failed(string error) =>
            new(PawnIoState.Unavailable, null, error);
    }

    private static InstallationInfo ReadInstallation()
    {
        Version? installedVersion = null;
        foreach (RegistryView view in
            new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    view);
                using RegistryKey? pawnIo = baseKey.OpenSubKey(UninstallRegistryPath);
                if (pawnIo is null)
                {
                    continue;
                }

                string? text = pawnIo.GetValue("DisplayVersion") as string;
                if (Version.TryParse(text, out Version? version))
                {
                    installedVersion = version;
                    break;
                }

                string? location = pawnIo.GetValue("InstallLocation") as string;
                string? uninstaller = string.IsNullOrWhiteSpace(location)
                    ? null
                    : Path.Combine(location, "uninstall.exe");
                string? fileVersion = File.Exists(uninstaller)
                    ? FileVersionInfo.GetVersionInfo(uninstaller).FileVersion
                    : null;

                installedVersion =
                    Version.TryParse(fileVersion, out version) ? version : null;
                break;
            }
            catch
            {
                // Try the other registry view. Installation will provide a
                // useful Windows error if neither view is readable.
            }
        }

        bool driverRegistered = false;
        try
        {
            using RegistryKey localMachine = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                RegistryView.Registry64);
            using RegistryKey? driver =
                localMachine.OpenSubKey(DriverRegistryPath);
            driverRegistered = driver is not null;
        }
        catch
        {
            // A failed verification causes one repair attempt. If that also
            // fails, the caller receives a concrete dependency error.
        }

        return new InstallationInfo(installedVersion, driverRegistered);
    }

    private sealed record InstallationInfo(
        Version? Version,
        bool DriverRegistered)
    {
        public bool IsUsable =>
            Version is not null &&
            Version >= RequiredVersion &&
            DriverRegistered;
    }
}
