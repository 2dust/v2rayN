using System.Text.Json.Nodes;
using ServiceLib;
using ServiceLib.Enums;
using ServiceLib.Models;
using ServiceLib.Services.CoreConfig;
using Xunit;

namespace ServiceLib.Tests;

public class CoreConfigV2rayServiceTests
{
    private const string SendThrough = "198.51.100.10";

    [Fact]
    public void GenerateClientConfigContent_OnlyAppliesSendThroughToRemoteProxyOutbounds()
    {
        var node = CreateProxyNode("proxy-1", "198.51.100.1", 443);
        var service = new CoreConfigV2rayService(CreateContext(node));

        var result = service.GenerateClientConfigContent();

        Assert.True(result.Success);

        var outbounds = GetOutbounds(result.Data?.ToString());
        var proxyOutbound = outbounds.Single(outbound => outbound["tag"]!.GetValue<string>() == Global.ProxyTag);
        var directOutbound = outbounds.Single(outbound => outbound["tag"]!.GetValue<string>() == Global.DirectTag);
        var blockOutbound = outbounds.Single(outbound => outbound["tag"]!.GetValue<string>() == Global.BlockTag);

        Assert.Equal(SendThrough, proxyOutbound["sendThrough"]?.GetValue<string>());
        Assert.Null(directOutbound["sendThrough"]);
        Assert.Null(blockOutbound["sendThrough"]);
    }

    [Fact]
    public void GenerateClientConfigContent_OnlyAppliesSendThroughToChainExitOutbounds()
    {
        var exitNode = CreateProxyNode("exit", "198.51.100.2", 443);
        var entryNode = CreateProxyNode("entry", "198.51.100.3", 443);
        var chainNode = CreateChainNode("chain", exitNode, entryNode);

        var service = new CoreConfigV2rayService(CreateContext(
            chainNode,
            allProxiesMap: new Dictionary<string, ProfileItem>
            {
                [exitNode.IndexId] = exitNode,
                [entryNode.IndexId] = entryNode,
            }));

        var result = service.GenerateClientConfigContent();

        Assert.True(result.Success);

        var outbounds = GetOutbounds(result.Data?.ToString())
            .Where(outbound => outbound["protocol"]?.GetValue<string>() is not ("freedom" or "blackhole" or "dns"))
            .ToList();

        var sendThroughOutbounds = outbounds
            .Where(outbound => outbound["sendThrough"]?.GetValue<string>() == SendThrough)
            .ToList();
        var chainedOutbounds = outbounds
            .Where(outbound => outbound["streamSettings"]?["sockopt"]?["dialerProxy"] is not null)
            .ToList();

        Assert.Single(sendThroughOutbounds);
        Assert.All(chainedOutbounds, outbound => Assert.Null(outbound["sendThrough"]));
    }

    [Fact]
    public void GenerateClientConfigContent_DoesNotApplySendThroughToTunRelayLoopbackOutbound()
    {
        var node = CreateProxyNode("proxy-1", "198.51.100.4", 443);
        var config = CreateConfig();
        config.TunModeItem.EnableLegacyProtect = false;

        var service = new CoreConfigV2rayService(CreateContext(
            node,
            config,
            isTunEnabled: true));

        var result = service.GenerateClientConfigContent();

        Assert.True(result.Success);

        var outbounds = GetOutbounds(result.Data?.ToString());
        Assert.DoesNotContain(outbounds, outbound => outbound["sendThrough"]?.GetValue<string>() == SendThrough);
    }

    private static CoreConfigContext CreateContext(
        ProfileItem node,
        Config? config = null,
        Dictionary<string, ProfileItem>? allProxiesMap = null,
        bool isTunEnabled = false)
    {
        return new CoreConfigContext
        {
            Node = node,
            RunCoreType = ECoreType.Xray,
            AppConfig = config ?? CreateConfig(),
            AllProxiesMap = allProxiesMap ?? new(),
            SimpleDnsItem = new SimpleDNSItem(),
            IsTunEnabled = isTunEnabled,
        };
    }

    private static Config CreateConfig()
    {
        return new Config
        {
            IndexId = string.Empty,
            SubIndexId = string.Empty,
            CoreBasicItem = new()
            {
                LogEnabled = false,
                Loglevel = "warning",
                MuxEnabled = false,
                DefAllowInsecure = false,
                DefFingerprint = Global.Fingerprints.First(),
                DefUserAgent = string.Empty,
                SendThrough = SendThrough,
                EnableFragment = false,
                EnableCacheFile4Sbox = true,
            },
            TunModeItem = new()
            {
                EnableTun = false,
                AutoRoute = true,
                StrictRoute = true,
                Stack = string.Empty,
                Mtu = 9000,
                EnableIPv6Address = false,
                IcmpRouting = Global.TunIcmpRoutingPolicies.First(),
                EnableLegacyProtect = false,
            },
            KcpItem = new(),
            GrpcItem = new(),
            RoutingBasicItem = new()
            {
                DomainStrategy = Global.DomainStrategies.First(),
                DomainStrategy4Singbox = Global.DomainStrategies4Sbox.First(),
                RoutingIndexId = string.Empty,
            },
            GuiItem = new(),
            MsgUIItem = new(),
            UiItem = new()
            {
                CurrentLanguage = "en",
                CurrentFontFamily = string.Empty,
                MainColumnItem = [],
                WindowSizeItem = [],
            },
            ConstItem = new(),
            SpeedTestItem = new(),
            Mux4RayItem = new()
            {
                Concurrency = 8,
                XudpConcurrency = 8,
                XudpProxyUDP443 = "reject",
            },
            Mux4SboxItem = new()
            {
                Protocol = string.Empty,
            },
            HysteriaItem = new(),
            ClashUIItem = new()
            {
                ConnectionsColumnItem = [],
            },
            SystemProxyItem = new(),
            WebDavItem = new(),
            CheckUpdateItem = new(),
            Fragment4RayItem = null,
            Inbound = [new InItem
            {
                Protocol = EInboundProtocol.socks.ToString(),
                LocalPort = 10808,
                UdpEnabled = true,
                SniffingEnabled = true,
                RouteOnly = false,
            }],
            GlobalHotkeys = [],
            CoreTypeItem = [],
            SimpleDNSItem = new(),
        };
    }

    private static ProfileItem CreateProxyNode(string indexId, string address, int port)
    {
        return new ProfileItem
        {
            IndexId = indexId,
            Remarks = indexId,
            ConfigType = EConfigType.SOCKS,
            CoreType = ECoreType.Xray,
            Address = address,
            Port = port,
        };
    }

    private static ProfileItem CreateChainNode(string indexId, params ProfileItem[] nodes)
    {
        var chainNode = new ProfileItem
        {
            IndexId = indexId,
            Remarks = indexId,
            ConfigType = EConfigType.ProxyChain,
            CoreType = ECoreType.Xray,
        };
        chainNode.SetProtocolExtra(new ProtocolExtraItem
        {
            ChildItems = string.Join(',', nodes.Select(node => node.IndexId)),
        });
        return chainNode;
    }

    private static List<JsonObject> GetOutbounds(string? json)
    {
        var root = JsonNode.Parse(json ?? throw new InvalidOperationException("Config JSON is missing"))?.AsObject()
            ?? throw new InvalidOperationException("Failed to parse config JSON");
        return root["outbounds"]?.AsArray().Select(node => node!.AsObject()).ToList()
            ?? throw new InvalidOperationException("Config JSON does not contain outbounds");
    }
}
