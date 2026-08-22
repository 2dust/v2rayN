namespace ServiceLib.Tests.CoreConfig.Context;

public class CoreConfigContextBuilderTests
{
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

    [Test]
    public async Task Build_WhenTunEnabled_ShouldAutoExcludeProxyServerAddress()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.RouteExcludeAddress = ["10.0.0.0/8"];
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "vmess-node");
        node.Address = "1.2.3.4";
        await UpsertProfilesAsync(node);

        var result = await CoreConfigContextBuilder.Build(config, node);

        await result.Success.Should().BeTrue();
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().Contain("10.0.0.0/8");
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().Contain("1.2.3.4/32");
    }

    [Test]
    public async Task Build_WhenTunEnabled_ShouldAutoExcludeProxyServerAddress_IPv6WithBrackets()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.TunModeItem.EnableTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "vmess-v6");
        node.Address = "[2001:db8::1]";
        await UpsertProfilesAsync(node);

        var result = await CoreConfigContextBuilder.Build(config, node);

        await result.Success.Should().BeTrue();
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().Contain("2001:db8::1/128");
    }

    [Test]
    public async Task Build_WhenTunEnabled_ShouldNotExcludeLoopbackAddress()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.TunModeItem.EnableTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "vmess-loopback");
        node.Address = "127.0.0.1";
        await UpsertProfilesAsync(node);

        var result = await CoreConfigContextBuilder.Build(config, node);

        await result.Success.Should().BeTrue();
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().NotContain("127.0.0.1/32");
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().NotContain("127.0.0.1");
    }

    [Test]
    public async Task Build_WhenTunEnabled_ShouldNotExcludeAnyOrNoneAddress()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.TunModeItem.EnableTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "vmess-any");
        node.Address = "0.0.0.0";
        await UpsertProfilesAsync(node);

        var result = await CoreConfigContextBuilder.Build(config, node);

        await result.Success.Should().BeTrue();
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().NotContain("0.0.0.0/32");
    }

    [Test]
    public async Task Build_WhenTunEnabled_WithDomainNode_ShouldExcludeResolvedIPs()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.TunModeItem.EnableTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "vmess-domain");
        node.Address = "one.one.one.one";
        await UpsertProfilesAsync(node);

        var result = await CoreConfigContextBuilder.Build(config, node);

        await result.Success.Should().BeTrue();
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().NotBeNull();
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress!.Count.Should().BeGreaterThan(0);
        await result.Context.AppConfig.TunModeItem.RouteExcludeAddress.Should().Contain(x => x.StartsWith("1.1.1.1") || x.StartsWith("1.0.0.1") || x.Contains(':'));
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

    private static readonly SemaphoreSlim _dbLock = new(1, 1);

    private static async Task UpsertProfilesAsync(params ProfileItem[] profiles)
    {
        await _dbLock.WaitAsync();
        try
        {
            SQLiteHelper.Instance.CreateTable<ProfileItem>();
            foreach (var profile in profiles)
            {
                await SQLiteHelper.Instance.ReplaceAsync(profile);
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }
}
