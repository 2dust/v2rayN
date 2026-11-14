namespace ServiceLib.Tests.CoreConfig.V2ray;

public class DnsFinalQueryTests
{
    [Test]
    [Arguments(1, 1)]
    [Arguments(1, 2)]
    [Arguments(2, 1)]
    [Arguments(2, 2)]
    [Arguments(2, 3)]
    [Arguments(3, 1)]
    [Arguments(3, 2)]
    [Arguments(3, 3)]
    public async Task ExpectedIps_ShouldTerminateAtLastResolverRegardlessOfFilterCount(int resolverCount, int filterCount)
    {
        string[] addresses = ["8.8.8.8", "8.8.4.4", "9.9.9.9"];
        string[] filters = ["geoip:cn", "192.0.2.0/24", "198.51.100.0/24"];
        foreach (var parallel in new[] { false, true })
        foreach (var tun in new[] { false, true })
        {
            var config = CoreConfigTestFactory.CreateConfig();
            config.SimpleDNSItem.DirectDNS = string.Join(',', addresses.Take(resolverCount));
            config.SimpleDNSItem.DirectExpectedIPs = string.Join(',', filters.Take(filterCount));
            config.SimpleDNSItem.ParallelQuery = parallel;
            config.TunModeItem.EnableTun = tun;
            var dns = GenerateDns(config);
            var filtered = dns["servers"]!.AsArray().OfType<JsonObject>()
                .Where(s => s["expectedIPs"] is JsonArray).ToList();

            await filtered.Count.Should().BeEqualTo(resolverCount);
            await filtered.Count(s => s["finalQuery"]?.GetValue<bool>() == true).Should().BeEqualTo(1);
            await (filtered.Last()["finalQuery"]?.GetValue<bool>() == true).Should().BeTrue();
            for (var i = 0; i < filtered.Count; i++)
            {
                await filtered[i]["address"]!.GetValue<string>().Should().BeEqualTo(addresses[i]);
                await filtered[i]["expectedIPs"]!.AsArray().Count.Should().BeEqualTo(filterCount);
                await filtered[i]["skipFallback"]!.GetValue<bool>().Should().BeTrue();
            }
            await dns["servers"]!.AsArray().OfType<JsonObject>()
                .Where(s => s["expectedIPs"] is null)
                .All(s => s["finalQuery"] is null).Should().BeTrue();
        }
    }

    [Test]
    public async Task DuplicateResolverAddresses_ShouldNotMoveTerminationBeforeLastDistinctResolver()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.SimpleDNSItem.DirectDNS = "8.8.8.8,8.8.8.8,8.8.4.4";
        config.SimpleDNSItem.DirectExpectedIPs = "geoip:cn,geoip:cn";
        var filtered = GenerateDns(config)["servers"]!.AsArray().OfType<JsonObject>()
            .Where(s => s["expectedIPs"] is JsonArray).ToList();
        await filtered.Count.Should().BeEqualTo(2);
        await (filtered[0]["finalQuery"]?.GetValue<bool>() == true).Should().BeFalse();
        await (filtered[1]["finalQuery"]?.GetValue<bool>() == true).Should().BeTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("192.0.2.0/24")]
    [Arguments("geoip:ru")]
    public async Task WithoutMatchingExpectedIpGroup_ShouldNotRestrictFallback(string? expectedIps)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.SimpleDNSItem.DirectDNS = "8.8.8.8,8.8.4.4";
        config.SimpleDNSItem.DirectExpectedIPs = expectedIps;
        var servers = GenerateDns(config)["servers"]!.AsArray().OfType<JsonObject>();
        await servers.All(s => s["expectedIPs"] is null && s["finalQuery"] is null).Should().BeTrue();
    }

    private static JsonNode GenerateDns(Config config)
    {
        config.SimpleDNSItem.RemoteDNS = "1.1.1.1";
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray) with
        {
            RoutingItem = new RoutingItem
            {
                DomainStrategy = Global.AsIs,
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new() { RuleType = ERuleType.DNS, Domain = ["geosite:cn", "domain:direct.example"], OutboundTag = Global.DirectTag },
                }),
            },
        };
        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Msg);
        }
        return JsonNode.Parse(result.Data!.ToString()!)!["dns"]!;
    }
}
