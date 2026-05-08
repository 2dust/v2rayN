using AwesomeAssertions;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Manager;
using ServiceLib.Models;
using ServiceLib.Services.CoreConfig;
using Xunit;

namespace ServiceLib.Tests.CoreConfig.Singbox;

public class CoreConfigSingboxServiceTests
{
    [Fact]
    public void GenerateClientConfigContent_ShouldGenerateBasicProxyConfig()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        result.Data.Should().NotBeNull();

        var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString());
        singboxConfig.Should().NotBeNull();
        singboxConfig!.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "socks");
        singboxConfig.inbounds.Should().Contain(i => i.type == nameof(EInboundProtocol.mixed));
    }

    [Fact]
    public void GenerateClientConfigContent_TunWithLoopbackPreSocks_ShouldKeepMixedInbound()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box);
        node.Address = Global.Loopback;
        node.Port = 1080;
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.inbounds.Should().Contain(i =>
            i.type == nameof(EInboundProtocol.mixed)
            && i.listen == Global.Loopback
            && i.listen_port == AppManager.Instance.GetLocalPort(EInboundProtocol.socks));
        cfg.inbounds.Should().Contain(i => i.type == "tun");
    }

    [Fact]
    public void GenerateClientConfigContent_BindInterface_ShouldUseDialBindInterface()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.CoreBasicItem.BindInterface = "eth0";
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);

        proxy.bind_interface.Should().Be("eth0");
        proxy.detour.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GenerateClientConfigContent_PolicyGroup_ShouldExpandChildrenAndBuildSelector()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n1", "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n2", "node-2");
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.sing_box, "g1", "group",
            [n1.IndexId, n2.IndexId]);

        var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.sing_box);
        context.AllProxiesMap[n1.IndexId] = n1;
        context.AllProxiesMap[n2.IndexId] = n2;
        context.AllProxiesMap[group.IndexId] = group;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "selector");
        cfg.outbounds.Should().Contain(o => o.tag == $"{Global.ProxyTag}-auto" && o.type == "urltest");
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-1-", StringComparison.Ordinal));
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-2-", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateClientConfigContent_ProxyChain_ShouldBuildDetourChain()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n1", "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n2", "node-2");
        var chain = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.sing_box, "c1", "chain",
            [n1.IndexId, n2.IndexId]);

        var context = CoreConfigTestFactory.CreateContext(config, chain, ECoreType.sing_box);
        context.AllProxiesMap[n1.IndexId] = n1;
        context.AllProxiesMap[n2.IndexId] = n2;
        context.AllProxiesMap[chain.IndexId] = chain;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "socks");
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-", StringComparison.Ordinal));
        cfg.outbounds.Should().Contain(o =>
            o.tag == Global.ProxyTag &&
            (o.detour ?? string.Empty).StartsWith("chain-proxy-1-", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateClientConfigContent_PolicyGroupWithProxyChain_ShouldBuildCombinedOutbounds()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n1", "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n2", "node-2");
        var n3 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n3", "node-3");
        var chain = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.sing_box, "c1", "chain",
            [n1.IndexId, n2.IndexId]);
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.sing_box, "g1", "group",
            [chain.IndexId, n3.IndexId]);

        var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.sing_box);
        context.AllProxiesMap[n1.IndexId] = n1;
        context.AllProxiesMap[n2.IndexId] = n2;
        context.AllProxiesMap[n3.IndexId] = n3;
        context.AllProxiesMap[chain.IndexId] = chain;
        context.AllProxiesMap[group.IndexId] = group;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "selector");
        cfg.outbounds.Should().Contain(o => o.tag == $"{Global.ProxyTag}-auto" && o.type == "urltest");
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-1-", StringComparison.Ordinal));
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-", StringComparison.Ordinal));
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-2-", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateClientConfigContent_ProxyChainWithPolicyGroup_ShouldBuildClonedChainBranches()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n1", "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n2", "node-2");
        var n3 = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n3", "node-3");
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.sing_box, "g1", "group",
            [n1.IndexId, n2.IndexId]);
        var chain = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.sing_box, "c1", "chain",
            [group.IndexId, n3.IndexId]);

        var context = CoreConfigTestFactory.CreateContext(config, chain, ECoreType.sing_box);
        context.AllProxiesMap[n1.IndexId] = n1;
        context.AllProxiesMap[n2.IndexId] = n2;
        context.AllProxiesMap[n3.IndexId] = n3;
        context.AllProxiesMap[group.IndexId] = group;
        context.AllProxiesMap[chain.IndexId] = chain;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "selector");
        cfg.outbounds.Should().Contain(o => o.tag == $"{Global.ProxyTag}-auto" && o.type == "urltest");
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-group-1-", StringComparison.Ordinal));
        cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-group-2-", StringComparison.Ordinal));

        var proxyCloneCount = cfg.outbounds.Count(o => o.tag.StartsWith("proxy-clone-", StringComparison.Ordinal));
        proxyCloneCount.Should().Be(2);

        var allCloneDetoursPointToGroupBranches = cfg.outbounds
            .Where(o => o.tag.StartsWith("proxy-clone-", StringComparison.Ordinal))
            .All(o => (o.detour ?? string.Empty).StartsWith("chain-proxy-1-group-", StringComparison.Ordinal));
        allCloneDetoursPointToGroupBranches.Should().BeTrue();
    }

    [Fact]
    public void GenerateClientConfigContent_RoutingSplit_DirectAndBlock_ShouldApplyRules()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "r-split-1",
                Remarks = "split-direct-block",
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.Routing,
                        OutboundTag = Global.DirectTag,
                        Domain = ["full:direct.example.com"],
                    },
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.Routing,
                        OutboundTag = Global.BlockTag,
                        Domain = ["full:block.example.com"],
                    }
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            }
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasDirectRule = cfg.route.rules.Any(r =>
            r.domain != null
            && r.domain.Contains("direct.example.com")
            && r.outbound == Global.DirectTag);
        hasDirectRule.Should().BeTrue();

        var hasBlockRule = cfg.route.rules.Any(r =>
            r.domain != null
            && r.domain.Contains("block.example.com")
            && r.action == "reject");
        hasBlockRule.Should().BeTrue();
    }

    [Fact]
    public void GenerateClientConfigContent_RoutingSplit_ByRemark_ShouldGenerateTargetOutbound()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var routeNode = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-route", "route-node");

        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "r-split-2",
                Remarks = "split-remark",
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.Routing,
                        OutboundTag = routeNode.Remarks,
                        Domain = ["full:route.example.com"],
                    }
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            }
        };
        context.AllProxiesMap[$"remark:{routeNode.Remarks}"] = routeNode;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var expectedPrefix = $"{routeNode.IndexId}-{Global.ProxyTag}-{routeNode.Remarks}";

        cfg.outbounds.Should().Contain(o => o.tag.StartsWith(expectedPrefix, StringComparison.Ordinal));

        var hasRouteRule = cfg.route.rules.Any(r =>
            r.domain != null
            && r.domain.Contains("route.example.com")
            && (r.outbound ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal));
        hasRouteRule.Should().BeTrue();
    }

    [Fact]
    public void GenerateClientConfigContent_DirectExpectedIPs_ShouldApplyGeoipAndCidrToDirectDnsRule()
    {
        var config = CoreConfigTestFactory.CreateConfigWithDirectExpectedIPs(
            ECoreType.sing_box,
            "192.168.0.0/16,geoip:cn");
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "r-dns-direct-expected",
                Remarks = "dns-direct-expected",
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.DNS,
                        OutboundTag = Global.DirectTag,
                        Domain = ["geosite:cn"],
                    }
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            }
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasExpectedRule = cfg.dns.rules?.Any(r =>
            r.server == Global.SingboxDirectDNSTag
            && r.ip_cidr?.Contains("192.168.0.0/16") == true
            && r.rule_set?.Contains("geosite-cn") == true
            && r.rule_set?.Contains("geoip-cn") == true) ?? false;

        hasExpectedRule.Should().BeTrue();
    }

    [Fact]
    public void GenerateClientConfigContent_BootstrapDNS_ShouldConfigurePureIPResolver()
    {
        var bootstrapDns = "8.8.8.8";
        var config = CoreConfigTestFactory.CreateConfigWithBootstrapDNS(ECoreType.sing_box, bootstrapDns);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        config.SimpleDNSItem.BootstrapDNS.Should().Be(bootstrapDns);

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var bootstrapServer = cfg.dns.servers?.FirstOrDefault(s => s.tag == Global.SingboxLocalDNSTag);
        bootstrapServer.Should().NotBeNull();
        (bootstrapServer?.server ?? string.Empty).Should().Contain(bootstrapDns);
    }

    [Fact]
    public void GenerateClientConfigContent_DnsFallback_LastRuleDirect_ShouldUseDirectFinalDns()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.SimpleDNSItem.DirectDNS = "1.1.1.1";
        config.SimpleDNSItem.RemoteDNS = "9.9.9.9";
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "r-direct-final",
                Remarks = "direct-final",
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.Routing,
                        OutboundTag = Global.DirectTag,
                        Ip = ["0.0.0.0/0"],
                        Port = "0-65535",
                        Network = "tcp,udp",
                    }
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            }
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.dns.final.Should().Be(Global.SingboxDirectDNSTag);
    }

    [Fact]
    public void GenerateClientConfigContent_DirectExpectedIPs_NonMatchingRegion_ShouldNotApplyExpectedRule()
    {
        var config =
            CoreConfigTestFactory.CreateConfigWithDirectExpectedIPs(ECoreType.sing_box, "192.168.0.0/16,geoip:cn");
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "r-dns-direct-unmatched",
                Remarks = "dns-direct-unmatched",
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.DNS,
                        OutboundTag = Global.DirectTag,
                        Domain = ["geosite:us"],
                    }
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            }
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasExpectedRule = cfg.dns.rules?.Any(r =>
            r.server == Global.SingboxDirectDNSTag
            && r.ip_cidr?.Contains("192.168.0.0/16") == true
            && r.rule_set?.Contains("geoip-cn") == true) ?? false;
        hasExpectedRule.Should().BeFalse();
    }

    [Theory]
    [InlineData("geosite:cn", "geosite-cn")]
    [InlineData("geosite:geolocation-cn", "geosite-geolocation-cn")]
    [InlineData("geosite:tld-cn", "geosite-tld-cn")]
    public void GenerateClientConfigContent_DirectExpectedIPs_RegionVariant_ShouldApplyExpectedRule(string domainTag,
        string expectedRuleSetTag)
    {
        var config =
            CoreConfigTestFactory.CreateConfigWithDirectExpectedIPs(ECoreType.sing_box, "192.168.0.0/16,geoip:cn");
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "r-dns-direct-variant",
                Remarks = "dns-direct-variant",
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true, RuleType = ERuleType.DNS, OutboundTag = Global.DirectTag, Domain = [domainTag],
                    }
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            }
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasExpectedRule = cfg.dns.rules?.Any(r =>
            r.server == Global.SingboxDirectDNSTag
            && r.ip_cidr?.Contains("192.168.0.0/16") == true
            && r.rule_set?.Contains(expectedRuleSetTag) == true
            && r.rule_set?.Contains("geoip-cn") == true) ?? false;
        hasExpectedRule.Should().BeTrue();
    }

    [Fact]
    public void GenerateClientConfigContent_Hosts_ShouldPopulateHostsServerAndDomainResolver()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.SimpleDNSItem.Hosts = "resolver.example 1.1.1.1";
        config.SimpleDNSItem.DirectDNS = "https://resolver.example/dns-query";
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hostsServer = cfg.dns.servers.FirstOrDefault(s => s.tag == Global.SingboxHostsDNSTag);
        hostsServer.Should().NotBeNull();
        hostsServer!.predefined.Should().ContainKey("resolver.example");
        hostsServer.predefined!["resolver.example"].Should().Contain("1.1.1.1");

        var directServer = cfg.dns.servers.FirstOrDefault(s => s.tag == Global.SingboxDirectDNSTag);
        directServer.Should().NotBeNull();
        directServer!.domain_resolver.Should().Be(Global.SingboxHostsDNSTag);
    }

    [Fact]
    public void GenerateClientConfigContent_RawDnsEnabled_ShouldUseCustomDnsAndInjectLocalResolver()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var rawDns = new Dns4Sbox
        {
            servers =
            [
                new Server4Sbox { tag = "remote", type = "udp", server = "8.8.8.8", detour = Global.ProxyTag, }
            ],
            rules = [],
        };
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            RawDnsItem = new DNSItem
            {
                Id = "dns-raw-1",
                Remarks = "raw",
                Enabled = true,
                CoreType = ECoreType.sing_box,
                NormalDNS = JsonUtils.Serialize(rawDns),
                DomainDNSAddress = "1.1.1.1",
            }
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        result.Success.Should().BeTrue($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        cfg.dns.servers.Should().Contain(s => s.tag == "remote" && s.type == "udp" && s.server == "8.8.8.8");
        cfg.dns.servers.Should().Contain(s => s.tag == Global.SingboxLocalDNSTag);
        cfg.dns.rules.Should().Contain(r => r.clash_mode == ERuleMode.Global.ToString());
        cfg.dns.rules.Should().Contain(r => r.clash_mode == ERuleMode.Direct.ToString());
    }
}
