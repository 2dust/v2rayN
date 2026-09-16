namespace ServiceLib.Tests.CoreConfig.V2ray;

public class CoreConfigV2rayBalancerFallbackTests
{
    private static V2rayConfig GenGroupConfig(EMultipleLoad load)
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "n1", "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, "n2", "node-2");
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, "g1", "group",
            [n1.IndexId, n2.IndexId]);
        group.SetProtocolExtra(group.GetProtocolExtra() with { MultipleLoad = load, });

        var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.Xray);
        context.AllProxiesMap[n1.IndexId] = n1;
        context.AllProxiesMap[n2.IndexId] = n2;
        context.AllProxiesMap[group.IndexId] = group;

        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        if (!result.Success)
        {
            throw new InvalidOperationException($"config gen failed: {result.Msg}");
        }
        return JsonUtils.Deserialize<V2rayConfig>(result.Data!.ToString())!;
    }

    [Test]
    public async Task LeastLoadBalancer_ShouldPinFallbackToFirstMember()
    {
        // #10174: leastLoad yields no candidate before burst observatory has
        // data; without fallbackTag Xray uses the default handler and traffic
        // leaks to the default proxy. The fallback must keep it in-group.
        var cfg = GenGroupConfig(EMultipleLoad.LeastLoad);

        await cfg.routing.balancers.Should().NotBeNull();
        var balancer = cfg.routing.balancers!.FirstOrDefault(
            b => b.tag == Global.ProxyTag + Global.BalancerTagSuffix);
        await balancer.Should().NotBeNull();
        await balancer!.strategy!.type.Should().BeEqualTo("leastLoad");
        await balancer.fallbackTag.Should().NotBeNull();
        await cfg.outbounds.Should().Contain(
            o => o.tag == balancer.fallbackTag,
            "fallback must point at a real group member outbound");
    }

    [Test]
    public async Task LeastPingBalancer_ShouldPinFallbackToFirstMember()
    {
        var cfg = GenGroupConfig(EMultipleLoad.LeastPing);

        var balancer = cfg.routing.balancers!.FirstOrDefault(
            b => b.tag == Global.ProxyTag + Global.BalancerTagSuffix);
        await balancer.Should().NotBeNull();
        await balancer!.fallbackTag.Should().NotBeNull();
        await cfg.outbounds.Should().Contain(o => o.tag == balancer.fallbackTag);
    }
}
