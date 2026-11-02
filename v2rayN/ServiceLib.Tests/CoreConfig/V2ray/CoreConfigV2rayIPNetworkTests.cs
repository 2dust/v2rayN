namespace ServiceLib.Tests.CoreConfig.V2ray;

public class CoreConfigV2rayIPNetworkTests
{
    [Test]
    [Arguments("0.0.0.0/0", "::/0")]
    [Arguments("::/0", "0.0.0.0/0")]
    [Arguments("0.0.0.0/1", "128.0.0.0/1,::/0")]
    [Arguments("::/1", "0.0.0.0/0,8000::/1")]
    public async Task GenerateClientConfigContent_TunExclusion_ShouldPreserveRemainingRoutes(
        string excludedNetwork, string expectedRoutes)
    {
        var config = CoreConfigTestFactory.CreateConfigWithTun(ECoreType.Xray, true);
        config.TunModeItem.RouteExcludeAddress = [excludedNetwork];
        var generated = await GenerateConfig(config);

        var routes = generated.inbounds.Single(i => i.protocol == "tun").settings.autoSystemRoutingTable;
        await routes.Should().BeEquivalentTo(expectedRoutes.Split(','), StringComparer.Ordinal);
    }

    [Test]
    public async Task GenerateClientConfigContent_TunExclusions_ShouldHandleOverlappingAndDuplicateNetworks()
    {
        var config = CoreConfigTestFactory.CreateConfigWithTun(ECoreType.Xray, true);
        config.TunModeItem.RouteExcludeAddress =
        [
            "0.0.0.0/1", "128.0.0.1/2", "128.0.0.1/2", "128.0.0.1/32",
            "::/1", "8000::1/2", "8000::1/128"
        ];
        var generated = await GenerateConfig(config);

        var routes = generated.inbounds.Single(i => i.protocol == "tun").settings.autoSystemRoutingTable;
        await routes.Should().BeEquivalentTo(new[] { "192.0.0.0/2", "c000::/2" }, StringComparer.Ordinal);
    }

    [Test]
    public async Task GenerateClientConfigContent_TunExclusions_ShouldAllowExcludingBothAddressFamilies()
    {
        var config = CoreConfigTestFactory.CreateConfigWithTun(ECoreType.Xray, true);
        config.TunModeItem.RouteExcludeAddress = ["0.0.0.0/0", "::/0"];
        var generated = await GenerateConfig(config);

        var routes = generated.inbounds.Single(i => i.protocol == "tun").settings.autoSystemRoutingTable;
        await routes.Should().BeEmpty();
    }

    [Test]
    public async Task GenerateClientConfigContent_TunHostExclusions_ShouldPreserveNeighbouringAddresses()
    {
        var config = CoreConfigTestFactory.CreateConfigWithTun(ECoreType.Xray, true);
        config.TunModeItem.RouteExcludeAddress =
        [
            "0.0.0.0/32", "255.255.255.255/32",
            "::/128", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128"
        ];
        var generated = await GenerateConfig(config);
        var routes = generated.inbounds.Single(i => i.protocol == "tun").settings.autoSystemRoutingTable
            .Select(IPNetwork.Parse).ToArray();

        foreach (var address in new[]
                 {
                     "0.0.0.0", "255.255.255.255", "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"
                 })
        {
            await routes.Any(route => route.Contains(IPAddress.Parse(address))).Should().BeFalse();
        }
        foreach (var address in new[]
                 {
                     "0.0.0.1", "255.255.255.254", "::1", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:fffe"
                 })
        {
            await routes.Any(route => route.Contains(IPAddress.Parse(address))).Should().BeTrue();
        }
    }

    [Test]
    [Arguments("198.18.0.0/15", 131071L)]
    [Arguments("0.0.0.0/0", 4294967295L)]
    [Arguments("198.18.0.0/31", 1L)]
    [Arguments("198.18.0.1/32", 65535L)]
    [Arguments("fd00::/64", long.MaxValue)]
    [Arguments("fd00::/66", 4611686018427387903L)]
    [Arguments("fd00::/127", 1L)]
    [Arguments("fd00::1/128", 65535L)]
    [Arguments("invalid-network", 65535L)]
    [Arguments("198.18.0.0/33", 65535L)]
    [Arguments("fd00::/129", 65535L)]
    public async Task GenerateClientConfigContent_FakeDns_ShouldDerivePoolSizeOrUseFallback(
        string range, long expectedPoolSize)
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        config.SimpleDNSItem.FakeIP = true;
        config.SimpleDNSItem.FakeIPRange = range;
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        context.RoutingItem!.RuleSet = JsonUtils.Serialize(new List<RulesItem>
        {
            new()
            {
                Enabled = true,
                RuleType = ERuleType.DNS,
                OutboundTag = Global.ProxyTag,
                Domain = ["full:example.com"],
            }
        });
        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        await result.Success.Should().BeTrue();
        var generated = JsonUtils.Deserialize<V2rayConfig>(result.Data!.ToString())!;

        await generated.fakedns.Should().NotBeNull();
        await generated.fakedns!.ipPool.Should().BeEqualTo(range);
        await generated.fakedns.poolSize.Should().BeEqualTo(expectedPoolSize);
    }

    private static async Task<V2rayConfig> GenerateConfig(Config config)
    {
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue();
        var generated = JsonUtils.Deserialize<V2rayConfig>(result.Data!.ToString());
        await generated.Should().NotBeNull();
        return generated!;
    }
}
