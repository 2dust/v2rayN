namespace ServiceLib.Common;

// Windows 默认出口读取集中在这里，避免旧版 TUN 保护的业务代码直接操作 Win32 路由结构。
// Keep Windows default-route interop here so legacy TUN protect logic does not handle Win32 route structs directly.
internal static class WindowsNetworkUtils
{
    private const string _tag = "WindowsNetworkUtils";
    private const ushort AF_INET = 2;

    public static IReadOnlyList<DefaultRouteCandidate> GetDefaultIPv4RouteCandidates()
    {
        var candidates = new List<DefaultRouteCandidate>();
        if (!OperatingSystem.IsWindows()
            || GetIpForwardTable2(AF_INET, out var table) != 0
            || table == IntPtr.Zero)
        {
            return candidates;
        }

        try
        {
            var count = (uint)Marshal.ReadInt32(table);
            var rowOffset = (int)Marshal.OffsetOf<MibIpForwardTable2>(nameof(MibIpForwardTable2.Table));
            var rowSize = Marshal.SizeOf<MibIpForwardRow2>();

            for (var i = 0; i < count; i++)
            {
                var rowPtr = IntPtr.Add(table, rowOffset + i * rowSize);
                var row = Marshal.PtrToStructure<MibIpForwardRow2>(rowPtr);
                if (row.DestinationPrefix.PrefixLength != 0)
                {
                    continue;
                }

                if (!TryGetInterfaceMetric(row.InterfaceLuid, row.InterfaceIndex, out var interfaceMetric, out var connected)
                    || !connected)
                {
                    continue;
                }

                candidates.Add(new DefaultRouteCandidate(
                    row.InterfaceIndex,
                    (ulong)row.Metric + interfaceMetric,
                    row.Metric,
                    interfaceMetric));
            }
        }
        catch (Exception ex)
        {
            candidates.Clear();
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            FreeMibTable(table);
        }

        return candidates
            .OrderBy(static candidate => candidate.TotalMetric)
            .ThenBy(static candidate => candidate.RouteMetric)
            .ThenBy(static candidate => candidate.InterfaceMetric)
            .ToList();
    }

    private static bool TryGetInterfaceMetric(ulong interfaceLuid,
        uint interfaceIndex,
        out uint metric,
        out bool connected)
    {
        metric = 0;
        connected = false;

        var row = new MibIpInterfaceRow
        {
            Family = AF_INET,
            InterfaceLuid = interfaceLuid,
            InterfaceIndex = interfaceIndex,
            ZoneIndices = new uint[16],
        };

        if (GetIpInterfaceEntry(ref row) != 0)
        {
            return false;
        }

        metric = row.Metric;
        connected = row.Connected;
        return true;
    }

    internal readonly record struct DefaultRouteCandidate(
        uint InterfaceIndex,
        ulong TotalMetric,
        uint RouteMetric,
        uint InterfaceMetric);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetIpForwardTable2(ushort family, out IntPtr table);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern void FreeMibTable(IntPtr memory);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetIpInterfaceEntry(ref MibIpInterfaceRow row);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibIpForwardTable2
    {
        public uint NumEntries;
        public MibIpForwardRow2 Table;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibIpForwardRow2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public IpAddressPrefix DestinationPrefix;
        public RawSockaddrInet NextHop;
        public byte SitePrefixLength;
        public uint ValidLifetime;
        public uint PreferredLifetime;
        public uint Metric;
        public uint Protocol;
        [MarshalAs(UnmanagedType.U1)] public bool Loopback;
        [MarshalAs(UnmanagedType.U1)] public bool AutoconfigureAddress;
        [MarshalAs(UnmanagedType.U1)] public bool Publish;
        [MarshalAs(UnmanagedType.U1)] public bool Immortal;
        public uint Age;
        public uint Origin;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct IpAddressPrefix
    {
        public RawSockaddrInet RawPrefix;
        public byte PrefixLength;
    }

    [StructLayout(LayoutKind.Sequential, Size = 28)]
    private struct RawSockaddrInet
    {
        public ushort Family;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibIpInterfaceRow
    {
        public ushort Family;
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public uint MaxReassemblySize;
        public ulong InterfaceIdentifier;
        public uint MinRouterAdvertisementInterval;
        public uint MaxRouterAdvertisementInterval;
        [MarshalAs(UnmanagedType.U1)] public bool AdvertisingEnabled;
        [MarshalAs(UnmanagedType.U1)] public bool ForwardingEnabled;
        [MarshalAs(UnmanagedType.U1)] public bool WeakHostSend;
        [MarshalAs(UnmanagedType.U1)] public bool WeakHostReceive;
        [MarshalAs(UnmanagedType.U1)] public bool UseAutomaticMetric;
        [MarshalAs(UnmanagedType.U1)] public bool UseNeighborUnreachabilityDetection;
        [MarshalAs(UnmanagedType.U1)] public bool ManagedAddressConfigurationSupported;
        [MarshalAs(UnmanagedType.U1)] public bool OtherStatefulConfigurationSupported;
        [MarshalAs(UnmanagedType.U1)] public bool AdvertiseDefaultRoute;
        public int RouterDiscoveryBehavior;
        public uint DadTransmits;
        public uint BaseReachableTime;
        public uint RetransmitTime;
        public uint PathMtuDiscoveryTimeout;
        public int LinkLocalAddressBehavior;
        public uint LinkLocalAddressTimeout;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] ZoneIndices;
        public uint SitePrefixLength;
        public uint Metric;
        public uint NlMtu;
        [MarshalAs(UnmanagedType.U1)] public bool Connected;
        [MarshalAs(UnmanagedType.U1)] public bool SupportsWakeUpPatterns;
        [MarshalAs(UnmanagedType.U1)] public bool SupportsNeighborDiscovery;
        [MarshalAs(UnmanagedType.U1)] public bool SupportsRouterDiscovery;
        public byte TransmitOffload;
        public byte ReceiveOffload;
        [MarshalAs(UnmanagedType.U1)] public bool DisableDefaultRoutes;
    }
}
