using System.Reflection;
using ServiceLib.Enums;
using ServiceLib.Manager;
using ServiceLib.Models;

namespace ServiceLib.Tests.CoreConfig;

internal static class CoreConfigTestFactory
{
    public static void BindAppManagerConfig(Config config)
    {
        var field = typeof(AppManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(AppManager.Instance, config);
    }

    public static Config CreateConfig(ECoreType vmessCoreType = ECoreType.Xray)
    {
        return new Config
        {
            CoreBasicItem = new CoreBasicItem { Loglevel = "warning", MuxEnabled = false },
            TunModeItem = new TunModeItem { EnableTun = false, IcmpRouting = "default" },
            KcpItem = new KcpItem(),
            GrpcItem = new GrpcItem(),
            RoutingBasicItem =
                new RoutingBasicItem
                {
                    DomainStrategy = Global.AsIs,
                    DomainStrategy4Singbox = string.Empty,
                    RoutingIndexId = string.Empty,
                },
            GuiItem = new GUIItem { EnableStatistics = false, DisplayRealTimeSpeed = false, EnableLog = false },
            MsgUIItem = new MsgUIItem(),
            UiItem =
                new UIItem
                {
                    CurrentLanguage = "en",
                    CurrentFontFamily = "sans",
                    MainColumnItem = [],
                    WindowSizeItem = []
                },
            ConstItem = new ConstItem(),
            SpeedTestItem = new SpeedTestItem
            {
                SpeedPingTestUrl = Global.SpeedPingTestUrls.First(),
                SpeedTestUrl = Global.SpeedTestUrls.First(),
                SpeedTestTimeout = 10,
                MixedConcurrencyCount = 1,
                IPAPIUrl = string.Empty,
            },
            Mux4RayItem = new Mux4RayItem { Concurrency = 8, XudpConcurrency = 16, XudpProxyUDP443 = "reject" },
            Mux4SboxItem = new Mux4SboxItem { Protocol = Global.SingboxMuxs.First(), MaxConnections = 8 },
            HysteriaItem = new HysteriaItem { UpMbps = 100, DownMbps = 100 },
            ClashUIItem = new ClashUIItem { ConnectionsColumnItem = [] },
            SystemProxyItem =
                new SystemProxyItem
                {
                    SystemProxyExceptions = string.Empty,
                    SystemProxyAdvancedProtocol = string.Empty
                },
            WebDavItem = new WebDavItem(),
            CheckUpdateItem = new CheckUpdateItem(),
            Fragment4RayItem = new Fragment4RayItem { Packets = "tlshello", Length = "100-200", Interval = "10-20" },
            Inbound =
            [
                new InItem
                {
                    Protocol = nameof(EInboundProtocol.socks),
                    LocalPort = 10808,
                    UdpEnabled = true,
                    SniffingEnabled = true,
                    RouteOnly = false,
                    DestOverride = ["http", "tls"],
                }
            ],
            GlobalHotkeys = [],
            CoreTypeItem =
            [
                new CoreTypeItem { ConfigType = EConfigType.VMess, CoreType = vmessCoreType }
            ],
            SimpleDNSItem = new SimpleDNSItem
            {
                BootstrapDNS = Global.DomainPureIPDNSAddress.FirstOrDefault(),
                ServeStale = false,
                ParallelQuery = false,
                Strategy4Freedom = Global.AsIs,
                Strategy4Proxy = Global.AsIs,
            },
            IndexId = string.Empty,
            SubIndexId = string.Empty,
        };
    }

    public static ProfileItem CreateVmessNode(ECoreType coreType, string indexId = "node-1", string remarks = "demo")
    {
        var node = new ProfileItem
        {
            IndexId = indexId,
            ConfigType = EConfigType.VMess,
            CoreType = coreType,
            Remarks = remarks,
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
            Subid = string.Empty,
        };

        node.SetProtocolExtra(node.GetProtocolExtra() with { AlterId = "0", VmessSecurity = Global.DefaultSecurity, });

        return node;
    }

    public static ProfileItem CreateSocksNode(ECoreType coreType, string indexId = "node-socks-1",
        string remarks = "demo-socks")
    {
        return new ProfileItem
        {
            IndexId = indexId,
            ConfigType = EConfigType.SOCKS,
            CoreType = coreType,
            Remarks = remarks,
            Address = "127.0.0.1",
            Port = 1080,
            Password = "pass",
            Username = "user",
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
            Subid = string.Empty,
        };
    }

    public static ProfileItem CreatePolicyGroupNode(ECoreType coreType, string indexId, string remarks,
        IEnumerable<string> childIndexIds)
    {
        var node = new ProfileItem
        {
            IndexId = indexId,
            ConfigType = EConfigType.PolicyGroup,
            CoreType = coreType,
            Remarks = remarks,
        };
        node.SetProtocolExtra(node.GetProtocolExtra() with
        {
            GroupType = nameof(EConfigType.PolicyGroup),
            ChildItems = string.Join(",", childIndexIds),
        });

        return node;
    }

    public static ProfileItem CreateProxyChainNode(ECoreType coreType, string indexId, string remarks,
        IEnumerable<string> childIndexIds)
    {
        var node = new ProfileItem
        {
            IndexId = indexId,
            ConfigType = EConfigType.ProxyChain,
            CoreType = coreType,
            Remarks = remarks,
        };
        node.SetProtocolExtra(node.GetProtocolExtra() with
        {
            GroupType = nameof(EConfigType.ProxyChain),
            ChildItems = string.Join(",", childIndexIds),
        });

        return node;
    }

    public static CoreConfigContext CreateContext(Config config, ProfileItem node, ECoreType runCoreType)
    {
        return new CoreConfigContext
        {
            Node = node,
            RunCoreType = runCoreType,
            AppConfig = config,
            RoutingItem = new RoutingItem
            {
                Id = "r1",
                Remarks = "default",
                RuleSet = "[]",
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            },
            RawDnsItem = null,
            SimpleDnsItem = config.SimpleDNSItem,
            AllProxiesMap = new Dictionary<string, ProfileItem> { [node.IndexId] = node },
            FullConfigTemplate = null,
            IsTunEnabled = false,
            ProtectDomainList = [],
        };
    }

    public static Config CreateConfigWithDirectExpectedIPs(ECoreType coreType,
        string directExpectedIPs = "192.168.0.0/16,geoip:cn")
    {
        var config = CreateConfig(coreType);
        config.SimpleDNSItem.DirectExpectedIPs = directExpectedIPs;
        return config;
    }

    public static Config CreateConfigWithBootstrapDNS(ECoreType coreType, string bootstrapDns = "8.8.8.8")
    {
        var config = CreateConfig(coreType);
        config.SimpleDNSItem.BootstrapDNS = bootstrapDns;
        return config;
    }
}
