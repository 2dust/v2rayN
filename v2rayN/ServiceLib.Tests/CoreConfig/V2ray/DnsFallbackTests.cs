namespace ServiceLib.Tests.CoreConfig.V2ray;

public class DnsFallbackTests
{
    [Test]
    [Arguments(null, false, false)]
    [Arguments(false, false, false)]
    [Arguments(true, false, false)]
    [Arguments(null, true, false)]
    [Arguments(false, true, false)]
    [Arguments(true, true, false)]
    [Arguments(null, false, true)]
    [Arguments(false, false, true)]
    [Arguments(true, false, true)]
    [Arguments(null, true, true)]
    [Arguments(false, true, true)]
    [Arguments(true, true, true)]
    public async Task SimpleDns_ShouldPersistAndApplyFallbackChoice(bool? disabled, bool parallel, bool tun)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.SimpleDNSItem.DisableFallbackIfMatch = disabled;
        config.SimpleDNSItem.ParallelQuery = parallel;
        config.SimpleDNSItem.DirectDNS = "8.8.8.8,8.8.4.4";
        config.SimpleDNSItem.RemoteDNS = "1.1.1.1";
        config.TunModeItem.EnableTun = tun;
        config = JsonUtils.Deserialize<Config>(JsonUtils.Serialize(config))!;
        await config.SimpleDNSItem.DisableFallbackIfMatch.Should().BeEqualTo(disabled);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray) with
        {
            RoutingItem = new RoutingItem
            {
                DomainStrategy = Global.AsIs,
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new() { RuleType = ERuleType.DNS, Domain = ["domain:direct.example"], OutboundTag = Global.DirectTag },
                }),
            },
        };

        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue();
        var dns = JsonNode.Parse(result.Data!.ToString()!)!["dns"]!;
        await (dns["disableFallbackIfMatch"]?.GetValue<bool>()).Should().BeEqualTo(disabled == true ? true : (bool?)null);
        await (dns["enableParallelQuery"]?.GetValue<bool>() == true).Should().BeEqualTo(parallel);
        var servers = dns["servers"]!.AsArray();
        var matched = servers.OfType<JsonObject>().Where(s => s["domains"] != null).ToList();
        await matched.Count.Should().BeEqualTo(2);
        await matched.All(s => s["skipFallback"]!.GetValue<bool>()).Should().BeTrue();
        await servers.Last()!.GetValue<string>().Should().BeEqualTo("1.1.1.1");
    }

    [Test]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    public async Task RawDns_ShouldUseItsOwnFallbackChoice(bool simpleChoice, bool rawChoice, bool tun)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.SimpleDNSItem.DisableFallbackIfMatch = simpleChoice;
        config.TunModeItem.EnableTun = tun;
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var raw = new JsonObject
        {
            ["servers"] = new JsonArray("1.1.1.1"),
            ["disableFallbackIfMatch"] = rawChoice,
        }.ToJsonString();
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray) with
        {
            RawDnsItem = new DNSItem { Enabled = true, NormalDNS = raw, TunDNS = raw },
        };

        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue();
        var dns = JsonNode.Parse(result.Data!.ToString()!)!["dns"]!;
        await dns["disableFallbackIfMatch"]!.GetValue<bool>().Should().BeEqualTo(rawChoice);
    }

    [Test]
    public async Task ExistingSettings_ShouldKeepFallbackEnabledByDefault()
    {
        var simple = JsonUtils.Deserialize<SimpleDNSItem>("{\"DirectDNS\":\"8.8.8.8\",\"RemoteDNS\":\"1.1.1.1\"}")!;
        await simple.DisableFallbackIfMatch.Should().BeNull();
    }

    [Test]
    public async Task Singbox_ShouldGenerateTheSameDnsForBothFallbackChoices()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);
        var before = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        config.SimpleDNSItem.DisableFallbackIfMatch = true;
        var after = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await before.Success.Should().BeTrue();
        await after.Success.Should().BeTrue();
        await JsonNode.DeepEquals(JsonNode.Parse(before.Data!.ToString()!)!["dns"],
            JsonNode.Parse(after.Data!.ToString()!)!["dns"]).Should().BeTrue();
    }
}
