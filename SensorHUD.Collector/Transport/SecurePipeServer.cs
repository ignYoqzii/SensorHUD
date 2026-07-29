using System.Buffers.Binary;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using SensorHUD.Core.Transport;
using Microsoft.Win32.SafeHandles;

namespace SensorHUD.Collector.Transport;

/// <summary>
/// Creates one package-scoped pipe endpoint at a time and verifies the process
/// behind every accepted connection. Messages cross into a process that may be
/// elevated, so the ACL and runtime identity check are both mandatory.
/// </summary>
internal sealed class SecurePipeServer
{
    private const int PipeBufferBytes = 64 * 1024;
    private const int MaximumPackageFamilyNameLength = 64;
    private const int AppContainerHashSubAuthorityCount = 7;
    private const uint SecurityDescriptorRevision = 1;
    private const uint LabelSecurityInformation = 0x00000010;
    private const int KernelObject = 6;
    private const string LowIntegrityLabelSddl = "S:(ML;;NW;;;LW)";

    private readonly PipeSecurity _security;
    private readonly string _packageFamilyName;

    public SecurePipeServer(string packageFamilyName)
    {
        _packageFamilyName = packageFamilyName;
        _security = CreateSecurity(packageFamilyName);
    }

    public NamedPipeServerStream Create()
    {
        return Create(GetPackagePipeName(_packageFamilyName));
    }

    private NamedPipeServerStream Create(string pipeName)
    {
        // LABEL_SECURITY_INFORMATION requires WRITE_OWNER on the handle.
        // Request it at creation; the DACL still controls every client.
        NamedPipeServerStream pipe = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            transmissionMode: PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous | PipeOptions.WriteThrough,
            inBufferSize: PipeBufferBytes,
            outBufferSize: PipeBufferBytes,
            pipeSecurity: _security,
            inheritability: HandleInheritability.None,
            additionalAccessRights: PipeAccessRights.TakeOwnership);

        try
        {
            // PipeSecurity only applies the DACL during creation. Apply the
            // mandatory label to the live kernel handle so a low-integrity
            // Game Bar AppContainer can pass the integrity check before its
            // package SID is evaluated by the DACL.
            ApplyLowIntegrityLabel(pipe);
            return pipe;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Confirms that the connected process belongs to this MSIX package. UWP
    /// pipe ACLs require a World rule for the token's normal SID evaluation, so
    /// the package rule alone cannot exclude ordinary desktop processes.
    /// </summary>
    public bool IsExpectedClient(NamedPipeServerStream pipe)
    {
        if (!NativeMethods.GetNamedPipeClientProcessId(
            pipe.SafePipeHandle,
            out uint processId))
        {
            return false;
        }

        using SafeProcessHandle process = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation,
            inheritHandle: false,
            processId);
        if (process.IsInvalid)
        {
            return false;
        }

        StringBuilder familyName =
            new(MaximumPackageFamilyNameLength + 1);
        uint length = (uint)familyName.Capacity;
        int result = NativeMethods.GetPackageFamilyName(
            process,
            ref length,
            familyName);
        bool matches = result == 0 &&
            string.Equals(
                familyName.ToString(),
                _packageFamilyName,
                StringComparison.Ordinal);
        return matches;
    }

    private static PipeSecurity CreateSecurity(string packageFamilyName)
    {
        PipeSecurity security = new();
        const PipeAccessRights clientRights =
            PipeAccessRights.ReadWrite |
            PipeAccessRights.Synchronize;

        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        // AppContainer restricted-token access checks require the requested
        // rights for both a normal SID and the exact package SID.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            clientRights,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            CreatePackageSid(packageFamilyName),
            clientRights,
            AccessControlType.Allow));

        // The service's initial handle is created directly. These rules allow
        // trusted operating-system administrators to inspect or recover it.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }

    /// <summary>
    /// Replaces only the kernel object's mandatory label. LABEL_SECURITY_INFORMATION
    /// is deliberately used instead of SACL_SECURITY_INFORMATION: the latter
    /// controls audit ACEs and requires a privilege an ordinary launch may not
    /// hold.
    /// </summary>
    private static void ApplyLowIntegrityLabel(NamedPipeServerStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        if (!NativeMethods.ConvertStringSecurityDescriptor(
            LowIntegrityLabelSddl,
            SecurityDescriptorRevision,
            out IntPtr descriptor,
            out _))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not create the pipe mandatory-label descriptor.");
        }

        try
        {
            if (!NativeMethods.GetSecurityDescriptorSacl(
                descriptor,
                out bool saclPresent,
                out IntPtr sacl,
                out _) ||
                !saclPresent ||
                sacl == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(
                    error,
                    "The pipe mandatory-label descriptor has no label ACL.");
            }

            uint result = NativeMethods.SetSecurityInfo(
                pipe.SafePipeHandle,
                KernelObject,
                LabelSecurityInformation,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                sacl);
            if (result != 0)
            {
                throw new Win32Exception(
                    checked((int)result),
                    "Could not apply the pipe mandatory-integrity label.");
            }
        }
        finally
        {
            _ = NativeMethods.LocalFree(descriptor);
        }
    }

    /// <summary>
    /// Creates the package AppContainer SID entirely in managed memory. Windows
    /// derives S-1-15-2 package SIDs from the first seven little-endian DWORDs
    /// of the SHA-256 hash of the lowercase UTF-16 package family name.
    /// Avoiding an allocated native PSID also avoids ownership and marshalling
    /// mistakes at this early service-security boundary.
    /// </summary>
    private static SecurityIdentifier CreatePackageSid(
        string packageFamilyName)
    {
        byte[] familyNameBytes = Encoding.Unicode.GetBytes(
            packageFamilyName.ToLowerInvariant());
        byte[] hash = SHA256.HashData(familyNameBytes);

        StringBuilder sid = new("S-1-15-2");
        for (int index = 0;
            index < AppContainerHashSubAuthorityCount;
            index++)
        {
            uint subAuthority = BinaryPrimitives.ReadUInt32LittleEndian(
                hash.AsSpan(index * sizeof(uint), sizeof(uint)));
            sid.Append('-').Append(subAuthority);
        }

        return new SecurityIdentifier(sid.ToString());
    }

    /// <summary>
    /// Resolves the pipe into the Game Bar widget's AppContainer namespace.
    /// The widget opens LOCAL\&lt;name&gt;, which Windows redirects to this
    /// session- and package-specific path. This explicit qualification is
    /// required because a full-trust server is outside the widget's
    /// AppContainer object namespace even though both share package identity.
    /// </summary>
    private static string GetPackagePipeName(string packageFamilyName)
    {
        const string localPrefix = "LOCAL\\";
        if (!CollectorProtocol.PipeName.StartsWith(
            localPrefix,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The packaged client pipe name must start with LOCAL\\.");
        }

        SecurityIdentifier packageSid = CreatePackageSid(packageFamilyName);
        byte[] sidBytes = new byte[packageSid.BinaryLength];
        packageSid.GetBinaryForm(sidBytes, 0);

        System.Runtime.InteropServices.GCHandle pinnedSid =
            System.Runtime.InteropServices.GCHandle.Alloc(
            sidBytes,
            System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            _ = NativeMethods.GetAppContainerNamedObjectPath(
                IntPtr.Zero,
                pinnedSid.AddrOfPinnedObject(),
                objectPathLength: 0,
                objectPath: IntPtr.Zero,
                out uint requiredLength);
            if (requiredLength == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(
                    error,
                    $"Could not determine the package named-object path. Error={error}.");
            }

            IntPtr objectPathBuffer =
                System.Runtime.InteropServices.Marshal.AllocHGlobal(
                checked((int)requiredLength * sizeof(char)));
            try
            {
                if (!NativeMethods.GetAppContainerNamedObjectPath(
                    IntPtr.Zero,
                    pinnedSid.AddrOfPinnedObject(),
                    requiredLength,
                    objectPathBuffer,
                    out _))
                {
                    throw new Win32Exception(
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                        "Could not resolve the package named-object path.");
                }

                string objectPath =
                    System.Runtime.InteropServices.Marshal.PtrToStringUni(
                        objectPathBuffer) ??
                    throw new InvalidOperationException(
                        "Windows returned an empty package named-object path.");
                string relativeObjectPath = objectPath.TrimStart('\\');
                if (!relativeObjectPath.StartsWith(
                    "Sessions\\",
                    StringComparison.OrdinalIgnoreCase))
                {
                    // With an explicit AppContainer SID and no AppContainer
                    // token, Windows returns \AppContainerNamedObjects\...
                    // without its per-session prefix. Named pipes expose that
                    // namespace below Sessions\<terminal-session-id>.
                    int sessionId =
                        System.Diagnostics.Process.GetCurrentProcess().SessionId;
                    relativeObjectPath =
                        $@"Sessions\{sessionId}\{relativeObjectPath}";
                }

                string pipeLeaf =
                    CollectorProtocol.PipeName[localPrefix.Length..];
                return $"{relativeObjectPath}\\{pipeLeaf}";
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(
                    objectPathBuffer);
            }
        }
        finally
        {
            pinnedSid.Free();
        }
    }

}
