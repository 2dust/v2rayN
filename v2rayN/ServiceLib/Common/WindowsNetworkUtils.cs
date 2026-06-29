namespace ServiceLib.Common;

// Keep Windows default-route interop here so legacy TUN protect code does not handle Win32 route structs directly.
// 将 Windows 默认路由互操作集中在这里，避免旧版 TUN 保护业务逻辑直接维护 Win32 路由结构。
internal static class WindowsNetworkUtils
{
    private const string _tag = "WindowsNetworkUtils";
    private const ushort AF_UNSPEC = 0;
    private const ushort AF_INET = 2;
    private const uint IfOperStatusUp = 1;
    private const uint IfTypeSoftwareLoopback = 24;
    private const uint IfTypePropVirtual = 53;
    private const int IfMaxStringSize = 256;
    private const int IfMaxPhysAddressLength = 32;
    private static readonly object NetworkChangeLock = new();
    private static IpForwardChangeCallback? _routeChangeCallback;
    private static IpInterfaceChangeCallback? _interfaceChangeCallback;
    private static IntPtr _routeChangeHandle;
    private static IntPtr _interfaceChangeHandle;
    private static bool _networkChangeMonitorStarted;

    public static event EventHandler? NetworkChanged;

    public static bool TryStartNetworkChangeMonitor()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        lock (NetworkChangeLock)
        {
            if (_networkChangeMonitorStarted)
            {
                return true;
            }

            _routeChangeCallback = OnRouteChanged;
            _interfaceChangeCallback = OnInterfaceChanged;

            var routeResult = NotifyRouteChange2(
                AF_UNSPEC,
                _routeChangeCallback,
                IntPtr.Zero,
                false,
                out _routeChangeHandle);
            if (routeResult != 0)
            {
                ResetNetworkChangeMonitor();
                Logging.SaveLog($"{_tag}: NotifyRouteChange2 failed: {routeResult}");
                return false;
            }

            var interfaceResult = NotifyIpInterfaceChange(
                AF_UNSPEC,
                _interfaceChangeCallback,
                IntPtr.Zero,
                false,
                out _interfaceChangeHandle);
            if (interfaceResult != 0)
            {
                ResetNetworkChangeMonitor();
                Logging.SaveLog($"{_tag}: NotifyIpInterfaceChange failed: {interfaceResult}");
                return false;
            }

            _networkChangeMonitorStarted = true;
            return true;
        }
    }

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

                if (!IsUsableDefaultRouteInterface(row.InterfaceLuid, row.InterfaceIndex))
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
                    (ulong)row.Metric + interfaceMetric));
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

        // sing-tun compares only the total metric; keep Windows route-table order on ties.
        // sing-tun 只比较总 metric；metric 相同时保留 Windows 路由表顺序。
        return candidates
            .OrderBy(static candidate => candidate.TotalMetric)
            .ToList();
    }

    private static bool TryGetInterfaceMetric(
        ulong interfaceLuid,
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

    private static bool IsUsableDefaultRouteInterface(ulong interfaceLuid, uint interfaceIndex)
    {
        var row = CreateMibIfRow2(interfaceLuid, interfaceIndex);
        if (GetIfEntry2(ref row) != 0)
        {
            return false;
        }

        return row.OperStatus == IfOperStatusUp
               && row.Type != IfTypePropVirtual
               && row.Type != IfTypeSoftwareLoopback;
    }

    private static MibIfRow2 CreateMibIfRow2(ulong interfaceLuid, uint interfaceIndex)
    {
        return new MibIfRow2
        {
            InterfaceLuid = interfaceLuid,
            InterfaceIndex = interfaceIndex,
            Alias = new ushort[IfMaxStringSize + 1],
            Description = new ushort[IfMaxStringSize + 1],
            PhysicalAddress = new byte[IfMaxPhysAddressLength],
            PermanentPhysicalAddress = new byte[IfMaxPhysAddressLength],
        };
    }

    private static void ResetNetworkChangeMonitor()
    {
        if (_routeChangeHandle != IntPtr.Zero)
        {
            CancelMibChangeNotify2(_routeChangeHandle);
            _routeChangeHandle = IntPtr.Zero;
        }

        if (_interfaceChangeHandle != IntPtr.Zero)
        {
            CancelMibChangeNotify2(_interfaceChangeHandle);
            _interfaceChangeHandle = IntPtr.Zero;
        }

        _routeChangeCallback = null;
        _interfaceChangeCallback = null;
        _networkChangeMonitorStarted = false;
    }

    private static void OnRouteChanged(IntPtr callerContext, IntPtr row, uint notificationType)
    {
        EmitNetworkChanged();
    }

    private static void OnInterfaceChanged(IntPtr callerContext, IntPtr row, uint notificationType)
    {
        EmitNetworkChanged();
    }

    private static void EmitNetworkChanged()
    {
        try
        {
            NetworkChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag}: network change callback failed", ex);
        }
    }

    internal readonly record struct DefaultRouteCandidate(
        uint InterfaceIndex,
        ulong TotalMetric);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void IpForwardChangeCallback(IntPtr callerContext, IntPtr row, uint notificationType);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void IpInterfaceChangeCallback(IntPtr callerContext, IntPtr row, uint notificationType);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int NotifyRouteChange2(
        ushort family,
        IpForwardChangeCallback callback,
        IntPtr callerContext,
        [MarshalAs(UnmanagedType.U1)] bool initialNotification,
        out IntPtr notificationHandle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int NotifyIpInterfaceChange(
        ushort family,
        IpInterfaceChangeCallback callback,
        IntPtr callerContext,
        [MarshalAs(UnmanagedType.U1)] bool initialNotification,
        out IntPtr notificationHandle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int CancelMibChangeNotify2(IntPtr notificationHandle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetIpForwardTable2(ushort family, out IntPtr table);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern void FreeMibTable(IntPtr memory);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetIpInterfaceEntry(ref MibIpInterfaceRow row);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetIfEntry2(ref MibIfRow2 row);

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

    [StructLayout(LayoutKind.Sequential, Size = 168)]
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
        public uint ReachableTime;
        public byte TransmitOffload;
        public byte ReceiveOffload;
        [MarshalAs(UnmanagedType.U1)] public bool DisableDefaultRoutes;
    }

    // Mirrors MIB_IF_ROW2 layout; only Type and OperStatus are used for sing-tun-style filtering.
    // 对齐 MIB_IF_ROW2 布局；这里只使用 Type 和 OperStatus 做 sing-tun 风格过滤。
    [StructLayout(LayoutKind.Sequential, Size = 1352)]
    private struct MibIfRow2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxStringSize + 1)]
        public ushort[] Alias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxStringSize + 1)]
        public ushort[] Description;
        public uint PhysicalAddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)]
        public byte[] PhysicalAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)]
        public byte[] PermanentPhysicalAddress;
        public uint Mtu;
        public uint Type;
        public uint TunnelType;
        public uint MediaType;
        public uint PhysicalMediumType;
        public uint AccessType;
        public uint DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public uint OperStatus;
        public uint AdminStatus;
        public uint MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets;
        public ulong InUcastPkts;
        public ulong InNUcastPkts;
        public ulong InDiscards;
        public ulong InErrors;
        public ulong InUnknownProtos;
        public ulong InUcastOctets;
        public ulong InMulticastOctets;
        public ulong InBroadcastOctets;
        public ulong OutOctets;
        public ulong OutUcastPkts;
        public ulong OutNUcastPkts;
        public ulong OutDiscards;
        public ulong OutErrors;
        public ulong OutUcastOctets;
        public ulong OutMulticastOctets;
        public ulong OutBroadcastOctets;
        public ulong OutQLen;
    }
}
