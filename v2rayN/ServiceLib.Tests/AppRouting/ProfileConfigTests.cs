using ServiceLib.Services.AppRouting;
using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.AppRouting;

public class ProfileConfigTests
{
    [Test]
    public async Task EffectiveTemplateChangesWithProfileSettingsButNotUnrelatedRules()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        var endpoint = new AppRouteRule { SocksUsername = "app-route", SocksPassword = "template", ApplyBlockingRules = true };
        string Template() => AppRouteProfileConfig.Generate(context, endpoint);
        var initial = Template();
        await Template().Should().BeEqualTo(initial);
        context.RoutingItem!.RuleSet = JsonUtils.Serialize(new[] { new RulesItem { OutboundTag = Global.DirectTag, Port = "443" } });
        await Template().Should().BeEqualTo(initial);
        node.Address = "changed.example";
        var address = Template();
        await address.Should().NotBeEqualTo(initial);
        node.Password = Guid.NewGuid().ToString();
        await Template().Should().NotBeEqualTo(address);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CommentedDomainsNeitherDropLiveBlockingRulesNorBroadenEmptyOnes(bool hasLiveDomain)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        var domains = new List<string>();
        if (hasLiveDomain)
        {
            domains.Add("domain:blocked.example");
        }
        domains.Add("# ignored comment at the end");
        context.RoutingItem!.RuleSet = JsonUtils.Serialize(new[]
        {
            new RulesItem { OutboundTag = Global.BlockTag, Domain = domains, Port = "443" }
        });
        var json = JsonNode.Parse(AppRouteProfileConfig.Generate(context, new() { SocksPort = 51997, ApplyBlockingRules = true }))!;
        var rules = json["routing"]!["rules"]!.AsArray();
        await rules.Count.Should().BeEqualTo(hasLiveDomain ? 2 : 1);
        if (hasLiveDomain)
        {
            await rules[0]!["outboundTag"]!.GetValue<string>().Should().BeEqualTo(Global.BlockTag);
            await rules[0]!["domain"]!.AsArray().Count.Should().BeEqualTo(1);
            await rules[0]!["domain"]![0]!.GetValue<string>().Should().BeEqualTo("domain:blocked.example");
        }
        await rules.Last()!["outboundTag"]!.GetValue<string>().Should().BeEqualTo(Global.ProxyTag);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OnlyEnabledTrafficBlockingRulesPrecedeTheSelectedProfile(bool applyBlockingRules)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.Inbound[0].SniffingEnabled = false;
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        context.RoutingItem!.DomainStrategy = Global.IPIfNonMatch;
        context.RoutingItem.RuleSet = JsonUtils.Serialize(new List<RulesItem>
        {
            new() { OutboundTag = Global.DirectTag, Network = "tcp,udp" },
            new() { OutboundTag = Global.BlockTag, Domain = ["domain:blocked.example"], Ip = ["192.0.2.0/24", "2001:db8::/32"] },
            new() { OutboundTag = Global.BlockTag, Port = "443", Network = "udp", InboundTag = ["socks"] },
            new() { OutboundTag = Global.BlockTag, Port = "80", Enabled = false },
            new() { OutboundTag = Global.BlockTag, Domain = ["domain:dns-only.example"], RuleType = ERuleType.DNS },
            new() { OutboundTag = Global.ProxyTag, Network = "tcp,udp" },
            new() { OutboundTag = "another-profile", Network = "tcp,udp" }
        });
        var original = JsonUtils.Serialize(context);
        var endpoint = new AppRouteRule { SocksPort = 51997, ApplyBlockingRules = applyBlockingRules };
        var json = JsonNode.Parse(AppRouteProfileConfig.Generate(context, endpoint))!;
        var rules = json["routing"]!["rules"]!.AsArray();
        await rules.Count.Should().BeEqualTo(applyBlockingRules ? 4 : 1);
        await rules.Last()!["outboundTag"]!.GetValue<string>().Should().BeEqualTo(Global.ProxyTag);
        await rules.Take(rules.Count - 1).All(r => r!["outboundTag"]!.GetValue<string>() == Global.BlockTag).Should().BeTrue();
        await json["outbounds"]!.AsArray().Any(o => o!["tag"]!.GetValue<string>() == Global.BlockTag &&
            o["protocol"]!.GetValue<string>() == "blackhole").Should().BeTrue();
        if (applyBlockingRules)
        {
            await rules[0]!["domain"]![0]!.GetValue<string>().Should().BeEqualTo("domain:blocked.example");
            await rules[1]!["ip"]!.AsArray().Count.Should().BeEqualTo(2);
            await rules[2]!["port"]!.GetValue<string>().Should().BeEqualTo("443");
            await rules[2]!["inboundTag"]![0]!.GetValue<string>().Should().BeEqualTo("socks");
            await json["inbounds"]![0]!["tag"]!.GetValue<string>().Should().BeEqualTo("socks");
            var sniffing = json["inbounds"]![0]!["sniffing"]!;
            await sniffing["enabled"]!.GetValue<bool>().Should().BeTrue();
            await sniffing["routeOnly"]!.GetValue<bool>().Should().BeTrue();
            await sniffing["destOverride"]!.AsArray().Select(s => s!.GetValue<string>()).SequenceEqual(new[] { "http", "tls", "quic" }).Should().BeTrue();
            await json["routing"]!["domainStrategy"]!.GetValue<string>().Should().BeEqualTo(Global.IPIfNonMatch);
            await rules.Last()!["ip"]!.AsArray().Select(ip => ip!.GetValue<string>()).SequenceEqual(new[] { "0.0.0.0/0", "::/0" }).Should().BeTrue();
        }
        else
        {
            await json["inbounds"]![0]!["sniffing"].Should().BeNull();
            await json["routing"]!["domainStrategy"]!.GetValue<string>().Should().BeEqualTo(Global.AsIs);
        }
        await JsonUtils.Serialize(context).Should().BeEqualTo(original);
    }

    [Test]
    public async Task BlockingSnapshotTracksTheSelectedRulesAndIgnoresUnrelatedEdits()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.RoutingBasicItem.DomainStrategy = Global.IPIfNonMatch;
        var blocked = new RulesItem { OutboundTag = Global.BlockTag, Domain = ["domain:first.example"] };
        var direct = new RulesItem { OutboundTag = Global.DirectTag, Domain = ["domain:direct.example"] };
        var routing = new RoutingItem { RuleSet = JsonUtils.Serialize(new[] { blocked, direct }) };
        var first = AppRouteProfileConfig.GetBlockingRouting(config, routing);
        await first.DomainStrategy.Should().BeEqualTo(Global.IPIfNonMatch);
        direct.Domain = ["domain:changed.example"];
        routing.RuleSet = JsonUtils.Serialize(new[] { blocked, direct });
        await JsonUtils.Serialize(AppRouteProfileConfig.GetBlockingRouting(config, routing)).Should().BeEqualTo(JsonUtils.Serialize(first));

        blocked.Domain = ["domain:second.example"];
        routing.RuleSet = JsonUtils.Serialize(new[] { blocked, direct });
        var changed = AppRouteProfileConfig.GetBlockingRouting(config, routing);
        await JsonUtils.Deserialize<List<RulesItem>>(changed.RuleSet)!.Single().Domain!.Single().Should().BeEqualTo("domain:second.example");
        await JsonUtils.Serialize(changed).Should().NotBeEqualTo(JsonUtils.Serialize(first));

        var empty = AppRouteProfileConfig.GetBlockingRouting(config, new RoutingItem { RuleSet = "[]" });
        await JsonUtils.Deserialize<List<RulesItem>>(empty.RuleSet)!.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task BlockingKeepsTheSelectedProfileGroupAsTheFinalBalancer()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        var first = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "first");
        var second = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "second");
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "group", "group", [first.IndexId, second.IndexId]);
        var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.Xray);
        context.AllProxiesMap[first.IndexId] = first;
        context.AllProxiesMap[second.IndexId] = second;
        context.RoutingItem!.RuleSet = JsonUtils.Serialize(new[] { new RulesItem { OutboundTag = Global.BlockTag, Port = "25" } });
        var endpoint = new AppRouteRule { SocksPort = 51997, ApplyBlockingRules = true };
        var json = JsonNode.Parse(AppRouteProfileConfig.Generate(context, endpoint))!;
        var rules = json["routing"]!["rules"]!.AsArray();
        await rules.Count.Should().BeEqualTo(2);
        await rules[0]!["outboundTag"]!.GetValue<string>().Should().BeEqualTo(Global.BlockTag);
        await rules[1]!["balancerTag"]!.GetValue<string>().Should().BeEqualTo(Global.ProxyTag + Global.BalancerTagSuffix);
        await json["outbounds"]!.AsArray().Count(o => o!["tag"]!.GetValue<string>().StartsWith("proxy-")).Should().BeEqualTo(2);
    }

    [Test]
    public async Task ProfileListenerIsIsolatedAuthenticatedAndSupportsUdpWithoutChangingMainConfig()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        config.TunModeItem.EnableTun = true;
        config.Mux4RayItem.XudpProxyUDP443 = "reject";
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        var endpoint = new AppRouteRule { SocksPort = 51997, SocksUsername = "fixture", SocksPassword = "fixture-secret" };
        var json = JsonNode.Parse(AppRouteProfileConfig.Generate(context, endpoint))!;
        var inbounds = json["inbounds"]!.AsArray();
        await inbounds.Count.Should().BeEqualTo(1);
        var inbound = inbounds[0]!;
        await inbound["listen"]!.GetValue<string>().Should().BeEqualTo("127.0.0.1");
        await inbound["port"]!.GetValue<int>().Should().BeEqualTo(endpoint.SocksPort);
        await inbound["protocol"]!.GetValue<string>().Should().BeEqualTo("socks");
        await inbound["settings"]!["udp"]!.GetValue<bool>().Should().BeTrue();
        await inbound["settings"]!["auth"]!.GetValue<string>().Should().BeEqualTo("password");
        await inbound["settings"]!["accounts"]![0]!["pass"]!.GetValue<string>().Should().BeEqualTo(endpoint.SocksPassword);
        await json["routing"]!["rules"]!.AsArray().Last()!["outboundTag"]!.GetValue<string>().Should().BeEqualTo(Global.ProxyTag);
        await config.TunModeItem.EnableTun.Should().BeTrue();
        await config.Mux4RayItem.XudpProxyUDP443.Should().BeEqualTo("reject");
        var muxes = json["outbounds"]!.AsArray().Select(o => o?["mux"]).Where(m => m?["enabled"]?.GetValue<bool>() == true);
        foreach (var mux in muxes)
        {
            await mux!["xudpProxyUDP443"]!.GetValue<string>().Should().BeEqualTo("allow");
        }
    }
}
