namespace ServiceLib.Tests.CoreConfig;

public class DualStackFakeIPTests
{
    [Test]
    [Arguments(null, "fc00::/18", 65535L)]
    [Arguments(" 2001:2::/48 ", "2001:2::/48", 65535L)]
    [Arguments("fc00::/120", "fc00::/120", 255L)]
    [Arguments("fc00::/127", "fc00::/127", 1L)]
    public async Task IPv6Range_ShouldUseAnXrayCompatiblePoolSize(string? input, string expectedRange, long expectedSize)
    {
        var valid = FakeIPUtils.TryParseIPv6Range(input, out var range, out var size);

        await valid.Should().BeTrue();
        await range.Should().BeEqualTo(expectedRange);
        await size.Should().BeEqualTo(expectedSize);
    }

    [Test]
    [Arguments("198.18.0.0/15")]
    [Arguments("not-a-network")]
    [Arguments("fc00::/128")]
    public async Task IPv6Range_ShouldRejectInvalidOrSingleAddressPools(string input)
    {
        await FakeIPUtils.TryParseIPv6Range(input, out _, out _).Should().BeFalse();
    }

    [Test]
    public async Task OldDnsSettings_ShouldKeepIPv6FakeIPDisabled()
    {
        var settings = JsonUtils.Deserialize<SimpleDNSItem>("""
            { "FakeIP": true, "FakeIPRange": "198.18.0.0/15" }
            """)!;

        await (settings.FakeIPv6 == true).Should().BeFalse();
        await settings.FakeIPRange.Should().BeEqualTo("198.18.0.0/15");
    }

    [Test]
    public async Task DnsSettings_ShouldRoundTripBothAddressRanges()
    {
        var settings = new SimpleDNSItem
        {
            FakeIP = true,
            FakeIPRange = "198.18.0.0/15",
            FakeIPv6 = true,
            FakeIPv6Range = "2001:2::/48",
        };

        var restored = JsonUtils.Deserialize<SimpleDNSItem>(JsonUtils.Serialize(settings))!;

        await restored.FakeIP.Should().BeEqualTo(true);
        await restored.FakeIPRange.Should().BeEqualTo(settings.FakeIPRange);
        await restored.FakeIPv6.Should().BeEqualTo(true);
        await restored.FakeIPv6Range.Should().BeEqualTo(settings.FakeIPv6Range);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Xray_ShouldAddIPv6OnlyWhenEnabledAndAAAAAllowed(bool enableIPv6, bool blockAAAA)
    {
        var context = CreateContext(ECoreType.Xray, enableIPv6, blockAAAA);
        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue();
        var root = JsonUtils.ParseJson(result.Data!.ToString())!;
        var dualStack = enableIPv6 && !blockAAAA;
        await (root["fakedns"] is JsonArray).Should().BeEqualTo(dualStack);
        var pool = dualStack ? root["fakedns"]![0]! : root["fakedns"]!;
        await pool["ipPool"]!.GetValue<string>().Should().BeEqualTo("198.18.0.0/15");
        if (dualStack)
        {
            await root["fakedns"]!.AsArray().Count.Should().BeEqualTo(2);
            await root["fakedns"]![1]!["ipPool"]!.GetValue<string>().Should().BeEqualTo("2001:2::/48");
            await root["fakedns"]![1]!["poolSize"]!.GetValue<long>().Should().BeEqualTo(65535L);
        }
        if (blockAAAA)
        {
            await root["dns"]!["queryStrategy"]!.GetValue<string>().Should().BeEqualTo("UseIPv4");
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Singbox_ShouldAddIPv6OnlyWhenEnabledAndAAAAAllowed(bool enableIPv6, bool blockAAAA)
    {
        var context = CreateContext(ECoreType.sing_box, enableIPv6, blockAAAA);
        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue();
        var config = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var fakeDns = config.dns.servers.Single(server => server.type == "fakeip");

        await fakeDns.inet4_range.Should().BeEqualTo("198.18.0.0/15");
        await fakeDns.inet6_range.Should().BeEqualTo(enableIPv6 && !blockAAAA ? "2001:2::/48" : null);
    }

    [Test]
    public async Task DisabledFakeIP_ShouldNotCreateEitherPool()
    {
        foreach (var coreType in new[] { ECoreType.Xray, ECoreType.sing_box })
        {
            var context = CreateContext(coreType, true, false);
            context.AppConfig.SimpleDNSItem.FakeIP = false;
            var result = coreType == ECoreType.Xray
                ? new CoreConfigV2rayService(context).GenerateClientConfigContent()
                : new CoreConfigSingboxService(context).GenerateClientConfigContent();

            await result.Success.Should().BeTrue();
            var root = JsonUtils.ParseJson(result.Data!.ToString())!;
            await root["fakedns"].Should().BeNull();
            await root["dns"]!["servers"]!.AsArray()
                .Any(server => server is JsonObject obj && obj["type"]?.GetValue<string>() == "fakeip")
                .Should().BeFalse();
        }
    }

    private static CoreConfigContext CreateContext(ECoreType coreType, bool enableIPv6, bool blockAAAA)
    {
        var config = CoreConfigTestFactory.CreateConfig(coreType);
        config.SimpleDNSItem.FakeIP = true;
        config.SimpleDNSItem.FakeIPRange = "198.18.0.0/15";
        config.SimpleDNSItem.FakeIPv6 = enableIPv6;
        config.SimpleDNSItem.FakeIPv6Range = "2001:2::/48";
        config.SimpleDNSItem.BlockAAAAQuery = blockAAAA;
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateVmessNode(coreType);
        return CoreConfigTestFactory.CreateContext(config, node, coreType) with
        {
            RoutingItem = new RoutingItem
            {
                Id = "fakeip-test",
                Remarks = "FakeIP test",
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
                RuleSet = JsonUtils.Serialize(new List<RulesItem>
                {
                    new()
                    {
                        Enabled = true,
                        RuleType = ERuleType.DNS,
                        OutboundTag = Global.ProxyTag,
                        Domain = ["full:example.com"],
                    },
                }),
            },
        };
    }
}
