namespace ServiceLib.Services.AppRouting;

// WinDivert 2.2 ABI: 16-byte header followed by a 64-byte union.
// In particular, NETWORK's 8 bytes do not determine the size of the union.
[StructLayout(LayoutKind.Explicit, Size = 80)]
internal struct DivertAddress
{
    [FieldOffset(0)] public long Timestamp;
    [FieldOffset(8)] public uint Flags;
    [FieldOffset(16)] public uint InterfaceIndex;
    [FieldOffset(20)] public uint SubInterfaceIndex;
    public bool Outbound
    {
        readonly get => (Flags & (1u << 17)) != 0; set => Flags = value ? Flags | (1u << 17) : Flags & ~(1u << 17);
    }
}

internal static class WinDivertApi
{
    private const string Library = "WinDivert.dll";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern IntPtr WinDivertOpen([MarshalAs(UnmanagedType.LPStr)] string filter, int layer, short priority, ulong flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinDivertRecv(IntPtr handle, [Out] byte[] packet, uint length, out uint received, out DivertAddress address);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinDivertSend(IntPtr handle, byte[] packet, uint length, out uint sent, ref DivertAddress address);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinDivertHelperCalcChecksums([In, Out] byte[] packet, uint length, ref DivertAddress address, ulong flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinDivertShutdown(IntPtr handle, uint how);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinDivertClose(IntPtr handle);
}
