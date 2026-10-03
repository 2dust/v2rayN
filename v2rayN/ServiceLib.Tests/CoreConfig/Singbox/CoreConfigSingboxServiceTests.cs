namespace ServiceLib.Tests.CoreConfig.Singbox;

public class CoreConfigSingboxServiceTests
{
    [Test]
    public async Task GenerateClientConfigContent_ShouldGenerateBasicProxyConfig()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        await result.Data.Should().NotBeNull();

        var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString());
        await singboxConfig.Should().NotBeNull();
        await singboxConfig!.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "socks");
        await singboxConfig.inbounds.Should().Contain(i => i.type == nameof(EInboundProtocol.mixed));
    }

    [Test]
    public async Task GenerateClientConfigContent_TunWithLoopbackPreSocks_ShouldKeepMixedInbound()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.inbounds.Should().Contain(i =>
            i.type == nameof(EInboundProtocol.mixed)
            && i.listen == Global.Loopback
            && i.listen_port == AppManager.Instance.GetLocalPort(EInboundProtocol.socks));
        await cfg.inbounds.Should().Contain(i => i.type == "tun");
    }

    [Test]
    public async Task GenerateClientConfigContent_TunEnabled_ShouldKeepEmbeddedTunRules()
    {
        // The embedded tun rules reject local-network noise (NetBIOS/mDNS, multicast).
        // They are deserialized into List<Rule4Sbox>, so a schema mismatch in the
        // embedded template makes JsonUtils.Deserialize return null and silently
        // drops every one of them.
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.route.rules.Should().Contain(
            r => r.action == "reject"
                && r.network != null && r.network.Contains("udp")
                && r.port != null && r.port.Contains(5353),
            "the embedded tun rules must reject mDNS/NetBIOS noise");
        await cfg.route.rules.Should().Contain(
            r => r.action == "reject"
                && r.ip_cidr != null && r.ip_cidr.Contains("224.0.0.0/3"),
            "the embedded tun rules must reject multicast traffic");
    }

    [Test]
    public async Task GenerateClientConfigContent_TunEnabled_ShouldRejectTrafficToTunOwnAddresses()
    {
        // Regression test: traffic addressed to the TUN interface's own addresses must
        // never reach an outbound. auto_route hijacks the default route, so `direct`
        // writes such a packet straight back into the TUN, which routes it to the
        // outbound again - an infinite loop that pins a CPU core. Observed in the wild
        // with WebRTC ICE connectivity checks against the TUN's own fc00::/7 ULA
        // address, sustaining ~8k packets/s out of the TUN interface.
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.EnableIPv6Address = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var tun = cfg.inbounds.First(i => i.type == "tun");
        //tun.address.Should().NotBeNullOrEmpty();
        await tun.address.Should().NotBeNull();
        await tun.address.Should().NotBeEmpty();

        foreach (var address in tun.address!)
        {
            var self = IPAddress.Parse(address.Split('/').First());
            var hostBits = self.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
            var expected = $"{self}/{hostBits}";
            await cfg.route.rules.Should().Contain(
                r => r.action == "reject" && r.ip_cidr != null && r.ip_cidr.Contains(expected),
                $"traffic to the TUN's own address '{address}' must be rejected, not routed");
        }

        // The match has to stay on the addresses themselves. sing-tun derives the TUN's DNS
        // entry from the address right after the interface's own, and every prefix offered
        // here leaves room for it, so a prefix match would drop system name lookups too.
        var dropRule = cfg.route.rules.First(r =>
            r.action == "reject" && r.method == "drop" && r.ip_cidr?.Count > 0);
        //dropRule.ip_cidr!.Should().OnlyContain(c =>
        //    c.EndsWith("/32", StringComparison.Ordinal) || c.EndsWith("/128", StringComparison.Ordinal));
        await dropRule.ip_cidr.Should().All(c => c.EndsWith("/32", StringComparison.Ordinal) || c.EndsWith("/128", StringComparison.Ordinal));
    }

    [Test]
    public async Task GenerateClientConfigContent_BindInterface_ShouldUseDialBindInterface()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);

        await proxy.bind_interface.Should().BeEqualTo("eth0");
        await proxy.detour.Should().BeNull().Or.BeEmpty();
    }

    [Test]
    public async Task GenerateClientConfigContent_PolicyGroup_ShouldExpandChildrenAndBuildSelector()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "selector");
        await cfg.outbounds.Should().Contain(o => o.tag == $"{Global.ProxyTag}-auto" && o.type == "urltest");
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-1-", StringComparison.Ordinal));
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-2-", StringComparison.Ordinal));
    }

    [Test]
    public async Task GenerateClientConfigContent_ProxyChain_ShouldBuildDetourChain()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "socks");
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-", StringComparison.Ordinal));
        await cfg.outbounds.Should().Contain(o =>
            o.tag == Global.ProxyTag &&
            (o.detour ?? string.Empty).StartsWith("chain-proxy-1-", StringComparison.Ordinal));
    }

    [Test]
    public async Task GenerateClientConfigContent_PolicyGroupWithProxyChain_ShouldBuildCombinedOutbounds()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "selector");
        await cfg.outbounds.Should().Contain(o => o.tag == $"{Global.ProxyTag}-auto" && o.type == "urltest");
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-1-", StringComparison.Ordinal));
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-", StringComparison.Ordinal));
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("proxy-2-", StringComparison.Ordinal));
    }

    [Test]
    public async Task GenerateClientConfigContent_ProxyChainWithPolicyGroup_ShouldBuildClonedChainBranches()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.outbounds.Should().Contain(o => o.tag == Global.ProxyTag && o.type == "selector");
        await cfg.outbounds.Should().Contain(o => o.tag == $"{Global.ProxyTag}-auto" && o.type == "urltest");
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-group-1-", StringComparison.Ordinal));
        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith("chain-proxy-1-group-2-", StringComparison.Ordinal));

        var proxyCloneCount = cfg.outbounds.Count(o => o.tag.StartsWith("proxy-clone-", StringComparison.Ordinal));
        await proxyCloneCount.Should().BeEqualTo(2);

        var allCloneDetoursPointToGroupBranches = cfg.outbounds
            .Where(o => o.tag.StartsWith("proxy-clone-", StringComparison.Ordinal))
            .All(o => (o.detour ?? string.Empty).StartsWith("chain-proxy-1-group-", StringComparison.Ordinal));
        await allCloneDetoursPointToGroupBranches.Should().BeTrue();
    }

    [Test]
    public async Task GenerateClientConfigContent_RoutingSplit_DirectAndBlock_ShouldApplyRules()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasDirectRule = cfg.route.rules.Any(r =>
            r.domain != null
            && r.domain.Contains("direct.example.com")
            && r.outbound == Global.DirectTag);
        await hasDirectRule.Should().BeTrue();

        var hasBlockRule = cfg.route.rules.Any(r =>
            r.domain != null
            && r.domain.Contains("block.example.com")
            && r.action == "reject");
        await hasBlockRule.Should().BeTrue();
    }

    [Test]
    public async Task GenerateClientConfigContent_RoutingSplit_ByRemark_ShouldGenerateTargetOutbound()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var expectedPrefix = $"{routeNode.IndexId}-{Global.ProxyTag}-{routeNode.Remarks}";

        await cfg.outbounds.Should().Contain(o => o.tag.StartsWith(expectedPrefix, StringComparison.Ordinal));

        var hasRouteRule = cfg.route.rules.Any(r =>
            r.domain != null
            && r.domain.Contains("route.example.com")
            && (r.outbound ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal));
        await hasRouteRule.Should().BeTrue();
    }

    [Test]
    public async Task GenerateClientConfigContent_BootstrapDNS_ShouldConfigurePureIPResolver()
    {
        var bootstrapDns = "8.8.8.8";
        var config = CoreConfigTestFactory.CreateConfigWithBootstrapDNS(ECoreType.sing_box, bootstrapDns);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        await config.SimpleDNSItem.BootstrapDNS.Should().BeEqualTo(bootstrapDns);

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var bootstrapServer = cfg.dns?.servers.FirstOrDefault(s => s.tag == Global.SingboxLocalDNSTag);
        await bootstrapServer.Should().NotBeNull();
        await bootstrapServer!.server.Should().Contain(bootstrapDns);
    }

    [Test]
    public async Task GenerateClientConfigContent_DnsFallback_LastRuleDirect_ShouldUseDirectFinalDns()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.dns.final.Should().BeEqualTo(Global.SingboxDirectDNSTag);
    }

    [Test]
    public async Task GenerateClientConfigContent_DirectExpectedIPs_NonMatchingRegion_ShouldNotApplyExpectedRule()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasExpectedRule = cfg.dns.rules?.Any(r =>
            r.server == Global.SingboxDirectDNSTag
            && r.ip_cidr?.Contains("192.168.0.0/16") == true
            && r.rule_set?.Contains("geoip-cn") == true) ?? false;
        await hasExpectedRule.Should().BeFalse();
    }

    [Test]
    [Arguments("geosite:cn", "geosite-cn")]
    [Arguments("geosite:geolocation-cn", "geosite-geolocation-cn")]
    [Arguments("geosite:tld-cn", "geosite-tld-cn")]
    public async Task GenerateClientConfigContent_DirectExpectedIPs_RegionVariant_ShouldApplyExpectedRule(string domainTag,
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
                        Enabled = true, RuleType = ERuleType.DNS, OutboundTag = Global.ProxyTag, Domain = ["geosite:google"],
                    },
                    new()
                    {
                        Enabled = true, RuleType = ERuleType.DNS, OutboundTag = Global.DirectTag, Domain = [domainTag],
                    },
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            },
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hasExpectedRule = (cfg.dns.rules?.Any(r =>
                                  r.ip_cidr?.Contains("192.168.0.0/16") == true
                                  && r.action == "respond"
                                  && r.rule_set?.Contains("geoip-cn") == true) ?? false)
                              && (cfg.dns.rules?.Any(r =>
                                  r.server == Global.SingboxDirectDNSTag
                                  && r.rule_set?.Contains(expectedRuleSetTag) == true
                                  && r.action == "evaluate") ?? false);
        await hasExpectedRule.Should().BeTrue();
    }

    [Test]
    public async Task GenerateClientConfigContent_Hosts_ShouldPopulateHostsServerAndDomainResolver()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.SimpleDNSItem.Hosts = "resolver.example 1.1.1.1";
        config.SimpleDNSItem.DirectDNS = "https://resolver.example/dns-query";
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "n-main", "main");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var hostsServer = cfg.dns.servers.FirstOrDefault(s => s.tag == Global.SingboxHostsDNSTag);
        await hostsServer.Should().NotBeNull();
        await hostsServer!.predefined.Should().ContainKey("resolver.example");
        await hostsServer.predefined!["resolver.example"].Should().Contain("1.1.1.1");

        var directServer = cfg.dns.servers.FirstOrDefault(s => s.tag == Global.SingboxDirectDNSTag);
        await directServer.Should().NotBeNull();
        await directServer!.domain_resolver.Should().BeEqualTo(Global.SingboxHostsDNSTag);
    }

    [Test]
    public async Task GenerateClientConfigContent_RawDnsEnabled_ShouldUseCustomDnsAndInjectLocalResolver()
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

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.dns.servers.Should().Contain(s => s.tag == "remote" && s.type == "udp" && s.server == "8.8.8.8");
        await cfg.dns.servers.Should().Contain(s => s.tag == Global.SingboxLocalDNSTag);
        await cfg.dns.rules.Should().Contain(r => r.clash_mode == nameof(ERuleMode.Global));
        await cfg.dns.rules.Should().Contain(r => r.clash_mode == nameof(ERuleMode.Direct));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task GenerateClientConfigContent_MultiDnsServers_ShouldIncludeAllServers(bool parallelQuery)
    {
        var config =
            CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.SimpleDNSItem.ParallelQuery = parallelQuery;
        config.SimpleDNSItem.DirectDNS = "1.1.1.1,2.2.2.2";
        config.SimpleDNSItem.RemoteDNS = "3.3.3.3,4.4.4.4";
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
                        Enabled = true,
                        RuleType = ERuleType.DNS,
                        OutboundTag = Global.ProxyTag,
                        Domain = ["geosite:google"],
                    },
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.DNS,
                        OutboundTag = Global.DirectTag,
                        Domain = ["geosite:cn"],
                    },
                }),
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            },
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        await cfg.dns.servers.Where(s => s.tag.StartsWith(Global.SingboxDirectDNSTagPrefix)).Should().HaveCount(2);
        await cfg.dns.servers.Where(s => s.tag.StartsWith(Global.SingboxRemoteDNSTagPrefix)).Should().HaveCount(2);
        if (parallelQuery)
        {
            await cfg.dns.rules.Should().Contain(r => r.race == true);
        }
    }

    [Test]
    public async Task GenerateClientConfigContent_Hysteria2Realm_ShouldEmitHttpsServerUrl()
    {
        var shareLink =
            "hysteria2+realm://public@realm.hy2.io/my-realm-id?auth=uuid&stun=turn.cloudflare.com%3A3478&sni=cloudflare.com&pinSHA256=xxx#Realm-Test";
        var node = Hysteria2Fmt.ResolveRealm(shareLink, out _);
        await node.Should().NotBeNull();
        node!.CoreType = ECoreType.sing_box;

        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.CoreTypeItem =
        [
            new CoreTypeItem { ConfigType = EConfigType.Hysteria2, CoreType = ECoreType.sing_box }
        ];
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);

        await proxy.type.Should().BeEqualTo("hysteria2");
        await proxy.realm.Should().NotBeNull();
        await proxy.realm!.server_url.Should().StartWith("https://");
        await proxy.realm.server_url.Should().Contain("realm.hy2.io");
        await proxy.realm.token.Should().BeEqualTo("public");
        await proxy.realm.realm_id.Should().BeEqualTo("my-realm-id");
        await proxy.realm.stun_servers.Should().Contain("turn.cloudflare.com:3478");
        await proxy.server.Should().BeNull();
    }

    [Test]
    public async Task GenerateClientConfigContent_TunSystemStackWithIpv6_ShouldUsePrefixWithPeerAddress()
    {
        // Regression test for #9820: sing-box fails with "need one more IPv6 address in
        // first prefix for system stack" when the TUN inbound uses a /128 IPv6 prefix.
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.Stack = "system";
        config.TunModeItem.EnableIPv6Address = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var tun = cfg.inbounds.First(i => i.type == "tun");

        await tun.address.Should().NotBeNull();
        await tun.address.Should().NotBeEmpty();
        foreach (var address in tun.address!)
        {
            var prefixLength = int.Parse(address[(address.LastIndexOf('/') + 1)..]);
            var isIpv6 = address.Contains(':');
            await prefixLength.Should().BeLessThanOrEqualTo(isIpv6 ? 126 : 30,
                $"'{address}' must leave room for the peer address the system stack derives");
        }
    }


    [Test]
    public async Task GenerateClientConfigContent_CustomOutbound_ShouldReplaceWithUserCustomOutboundJson()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var customNode = CoreConfigTestFactory.CreateCustomOutboundNode(ECoreType.sing_box, "n-custom", "custom-singbox");
        var customJsonContent = """
        {
          "type": "shadowsocks",
          "server": "1.2.3.4",
          "server_port": 8388,
          "method": "aes-128-gcm",
          "password": "custom_password"
        }
        """;

        var context = CoreConfigTestFactory.CreateContext(config, customNode, ECoreType.sing_box);
        context.CustomOutboundContent[customNode.IndexId] = customJsonContent;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        await result.Data.Should().NotBeNull();

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString());
        await cfg.Should().NotBeNull();
        var proxyOutbound = cfg!.outbounds.FirstOrDefault(o => o.tag == Global.ProxyTag);
        await proxyOutbound.Should().NotBeNull();
        await proxyOutbound!.type.Should().BeEqualTo("shadowsocks");
        await proxyOutbound.server.Should().BeEqualTo("1.2.3.4");
        await proxyOutbound.server_port.Should().BeEqualTo(8388);
        await proxyOutbound.method.Should().BeEqualTo("aes-128-gcm");
        await proxyOutbound.password.Should().BeEqualTo("custom_password");
    }
}
