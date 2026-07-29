using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace SensorHUD.Collector.Transport;

/// <summary>
/// Native Windows declarations used by the collector. Keeping declarations in
/// one reviewed file makes security-sensitive handle and marshalling behavior
/// easier to audit.
/// </summary>
internal static class NativeMethods
{
    internal const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetAppContainerNamedObjectPath(
        IntPtr token,
        IntPtr appContainerSid,
        uint objectPathLength,
        IntPtr objectPath,
        out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(
        int desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetPackageFamilyName(
        SafeProcessHandle process,
        ref uint packageFamilyNameLength,
        StringBuilder packageFamilyName);

    [DllImport(
        "advapi32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint =
            "ConvertStringSecurityDescriptorToSecurityDescriptorW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ConvertStringSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSecurityDescriptorRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSecurityDescriptorSacl(
        IntPtr securityDescriptor,
        [MarshalAs(UnmanagedType.Bool)] out bool saclPresent,
        out IntPtr sacl,
        [MarshalAs(UnmanagedType.Bool)] out bool saclDefaulted);

    [DllImport("advapi32.dll")]
    internal static extern uint SetSecurityInfo(
        SafePipeHandle handle,
        int objectType,
        uint securityInformation,
        IntPtr owner,
        IntPtr group,
        IntPtr dacl,
        IntPtr sacl);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(
        ref MemoryStatus memoryStatus);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(
        nint window,
        out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public static MemoryStatus Create() => new()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatus>(),
        };
    }
}
