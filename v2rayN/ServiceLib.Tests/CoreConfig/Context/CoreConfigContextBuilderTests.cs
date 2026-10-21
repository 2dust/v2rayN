namespace ServiceLib.Tests.CoreConfig.Context;

public class CoreConfigContextBuilderTests
{
    [Test]
    [Arguments(EMultipleLoad.LeastPing, "leastPing")]
    [Arguments(EMultipleLoad.Fallback, "leastLoad")]
    [Arguments(EMultipleLoad.Random, "random")]
    [Arguments(EMultipleLoad.RoundRobin, "roundRobin")]
    [Arguments(EMultipleLoad.LeastLoad, "leastLoad")]
    public async Task ResolveNodeAsync_SubscriptionChain_ShouldPreservePolicyGroupStrategy(
        EMultipleLoad multipleLoad, string strategyType)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        SQLiteHelper.Instance.CreateTable<SubItem>();

        var subId = NewId("sub");
        var n1 = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, NewId("node"), "node-1");
        var n2 = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, NewId("node"), "node-2");
        n1.Subid = subId;
        n2.Subid = subId;
        var prev = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, NewId("prev"), NewId("prev"));
        var next = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, NewId("next"), NewId("next"));
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, NewId("group"), "group", []);
        group.Subid = subId;
        group.SetProtocolExtra(group.GetProtocolExtra() with
        {
            MultipleLoad = multipleLoad,
            SubChildItems = subId,
            Filter = "^node-",
        });
        await UpsertProfilesAsync(n1, n2, prev, next, group);

        foreach (var (hasPrev, hasNext) in new[] { (true, false), (false, true), (true, true) })
        {
            await SQLiteHelper.Instance.ReplaceAsync(new SubItem
            {
                Id = subId,
                Remarks = "sub",
                PrevProfile = hasPrev ? prev.Remarks : null,
                NextProfile = hasNext ? next.Remarks : null,
            });
            var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.Xray);
            context.AllProxiesMap.Clear();
            var (resolvedNode, validatorResult) = await CoreConfigContextBuilder.ResolveNodeAsync(context,
                JsonUtils.DeepCopy(group));

            await validatorResult.Success.Should().BeTrue();
            await resolvedNode.ConfigType.Should().BeEqualTo(EConfigType.ProxyChain);

            var result = new CoreConfigV2rayService(context with { Node = resolvedNode }).GenerateClientConfigContent();
            await result.Success.Should().BeTrue();
            var cfg = JsonUtils.Deserialize<V2rayConfig>(result.Data!.ToString())!;
            var balancer = cfg.routing.balancers!.Single(b => b.tag == Global.ProxyTag + Global.BalancerTagSuffix);
            await balancer.strategy.type.Should().BeEqualTo(strategyType);
            if (multipleLoad == EMultipleLoad.Fallback)
            {
                await balancer.strategy.settings!.tolerance.Should().BeEqualTo(0.2);
                await balancer.strategy.settings.maxRTT.Should().BeEqualTo("5000ms");
            }
        }
    }

    [Test]
    public async Task ResolveNodeAsync_DirectCycleDependency_ShouldFailWithCycleError()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var groupAId = NewId("group-a");
        var groupBId = NewId("group-b");
        var groupA = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupAId, "group-a", [groupBId]);
        var groupB = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupBId, "group-b", [groupAId]);

        await UpsertProfilesAsync(groupA, groupB);

        var context = CoreConfigTestFactory.CreateContext(config, groupA, ECoreType.Xray);
        context.AllProxiesMap.Clear();

        var (_, validatorResult) = await CoreConfigContextBuilder.ResolveNodeAsync(context, groupA, false);

        await validatorResult.Success.Should().BeFalse();
        await validatorResult.Errors.Should().Contain(ContainsCycleDependencyMessage);
        await context.AllProxiesMap.Should().NotContainKey(groupA.IndexId);
        await context.AllProxiesMap.Should().NotContainKey(groupB.IndexId);
    }

    [Test]
    public async Task ResolveNodeAsync_IndirectCycleDependency_ShouldFailWithCycleError()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var groupAId = NewId("group-a");
        var groupBId = NewId("group-b");
        var groupCId = NewId("group-c");
        var groupA = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupAId, "group-a", [groupBId]);
        var groupB = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupBId, "group-b", [groupCId]);
        var groupC = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupCId, "group-c", [groupAId]);

        await UpsertProfilesAsync(groupA, groupB, groupC);

        var context = CoreConfigTestFactory.CreateContext(config, groupA, ECoreType.Xray);
        context.AllProxiesMap.Clear();

        var (_, validatorResult) = await CoreConfigContextBuilder.ResolveNodeAsync(context, groupA, false);

        await validatorResult.Success.Should().BeFalse();
        await validatorResult.Errors.Should().Contain(ContainsCycleDependencyMessage);
        await context.AllProxiesMap.Should().NotContainKey(groupA.IndexId);
        await context.AllProxiesMap.Should().NotContainKey(groupB.IndexId);
        await context.AllProxiesMap.Should().NotContainKey(groupC.IndexId);
    }

    [Test]
    public async Task ResolveNodeAsync_CycleWithValidBranch_ShouldSkipCycleAndKeepValidChild()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var groupAId = NewId("group-a");
        var groupBId = NewId("group-b");
        var leafId = NewId("leaf");
        var groupA = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupAId, "group-a", [groupBId, leafId]);
        var groupB = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.Xray, groupBId, "group-b", [groupAId]);
        var leaf = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, leafId, "leaf");

        await UpsertProfilesAsync(groupA, groupB, leaf);

        var context = CoreConfigTestFactory.CreateContext(config, groupA, ECoreType.Xray);
        context.AllProxiesMap.Clear();

        var (_, validatorResult) = await CoreConfigContextBuilder.ResolveNodeAsync(context, groupA, false);

        await validatorResult.Success.Should().BeTrue();
        await validatorResult.Errors.Should().BeEmpty();
        await validatorResult.Warnings.Should().Contain(ContainsCycleDependencyMessage);

        await context.AllProxiesMap.Should().ContainKey(leaf.IndexId);
        await context.AllProxiesMap.Should().ContainKey(groupA.IndexId);
        await context.AllProxiesMap.Should().NotContainKey(groupB.IndexId);
        await groupA.GetProtocolExtra().ChildItems.Should().BeEqualTo(leaf.IndexId);
    }

    private static string NewId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private static bool ContainsCycleDependencyMessage(string message)
    {
        return message.Contains("cycle dependency", StringComparison.OrdinalIgnoreCase)
               || message.Contains("循环依赖", StringComparison.Ordinal)
               || message.Contains("循環依賴", StringComparison.Ordinal)
               || message.Contains("циклическую зависимость", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task UpsertProfilesAsync(params ProfileItem[] profiles)
    {
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        foreach (var profile in profiles)
        {
            await SQLiteHelper.Instance.ReplaceAsync(profile);
        }
    }
}
