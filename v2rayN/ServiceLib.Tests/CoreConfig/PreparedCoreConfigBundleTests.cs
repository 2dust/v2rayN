namespace ServiceLib.Tests.CoreConfig;

[NotInParallel]
public class PreparedCoreConfigBundleTests
{
    [Test]
    public async Task EquivalentJsonFormatting_DoesNotRequireReload_ButRuleOrderDoes()
    {
        var source = Path.Combine(Path.GetTempPath(), $"v2rayn-bundle-{Guid.NewGuid():N}.json");
        try
        {
            var config = CoreConfigTestFactory.CreateConfig();
            var node = new ProfileItem { IndexId = "custom", ConfigType = EConfigType.Custom, CoreType = ECoreType.Xray, Address = source };
            var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
            await File.WriteAllTextAsync(source, "{\"port\":1080,\"rules\":[\"first\",\"second\"]}");
            var running = await PreparedCoreConfigBundle.CreateAsync(context, null);
            await File.WriteAllTextAsync(source, "{\"rules\": [\"first\", \"second\"], \"port\": 1080}");
            var equivalent = await PreparedCoreConfigBundle.CreateAsync(context, null);
            await equivalent.IsEquivalentTo(running).Should().BeTrue();

            await File.WriteAllTextAsync(source, "{\"port\":1080,\"rules\":[\"second\",\"first\"]}");
            var reordered = await PreparedCoreConfigBundle.CreateAsync(context, null);
            await reordered.IsEquivalentTo(running).Should().BeFalse();
        }
        finally { File.Delete(source); }
    }

    [Test]
    [Arguments(ECoreType.Xray)]
    [Arguments(ECoreType.sing_box)]
    public async Task GeneratedConfig_IgnoresUiIdentityButDetectsCredentialsAndPreSocksChanges(ECoreType core)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(core);
        var main = CoreConfigTestFactory.CreateContext(config, node, core);
        var running = await PreparedCoreConfigBundle.CreateAsync(main, null);
        node.IndexId = "recreated-id";
        node.Remarks = "renamed display label";
        var equivalent = await PreparedCoreConfigBundle.CreateAsync(main, null);
        await equivalent.IsEquivalentTo(running).Should().BeTrue();

        node.Password = "changed-password";
        var changed = await PreparedCoreConfigBundle.CreateAsync(main, null);
        await changed.IsEquivalentTo(running).Should().BeFalse();
        node.Password = "pass";
        var preNode = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "pre");
        var pre = CoreConfigTestFactory.CreateContext(config, preNode, ECoreType.sing_box) with { IsTunEnabled = true };
        var withPre = await PreparedCoreConfigBundle.CreateAsync(main, pre);
        await withPre.IsEquivalentTo(running).Should().BeFalse();
        preNode.Port++;
        var changedPre = await PreparedCoreConfigBundle.CreateAsync(main, pre);
        await changedPre.IsEquivalentTo(withPre).Should().BeFalse();
    }

    [Test]
    public async Task GeneratedTunAndParallelDns_AreRepeatableForUnchangedInputs()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.SimpleDNSItem.ParallelQuery = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);
        foreach (var core in new[] { ECoreType.Xray, ECoreType.sing_box })
        {
            var node = CoreConfigTestFactory.CreateSocksNode(core);
            var context = CoreConfigTestFactory.CreateContext(config, node, core) with { IsMacOS = true, IsTunEnabled = true };
            var first = await PreparedCoreConfigBundle.CreateAsync(context, null);
            for (var i = 0; i < 5; i++)
            {
                var next = await PreparedCoreConfigBundle.CreateAsync(context, null);
                await next.IsEquivalentTo(first).Should().BeTrue();
            }
        }
    }

    [Test]
    [Arguments(ECoreType.Xray, false)]
    [Arguments(ECoreType.Xray, true)]
    [Arguments(ECoreType.sing_box, false)]
    [Arguments(ECoreType.sing_box, true)]
    public async Task ChildDisplayRename_PreservesGroupOrChain_ButOrderAndEndpointChangesDoNot(ECoreType core, bool chain)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var first = CoreConfigTestFactory.CreateSocksNode(core, "first", "first label");
        var second = CoreConfigTestFactory.CreateSocksNode(core, "second", "second label");
        second.Port = 1081;
        var group = chain
            ? CoreConfigTestFactory.CreateProxyChainNode(core, "group", "chain", [first.IndexId, second.IndexId])
            : CoreConfigTestFactory.CreatePolicyGroupNode(core, "group", "group", [first.IndexId, second.IndexId]);
        var context = CoreConfigTestFactory.CreateContext(config, group, core) with
        {
            AllProxiesMap = new() { [group.IndexId] = group, [first.IndexId] = first, [second.IndexId] = second }
        };
        var running = await PreparedCoreConfigBundle.CreateAsync(context, null);
        first.Remarks = "renamed display only";
        var renamed = await PreparedCoreConfigBundle.CreateAsync(context, null);
        await renamed.IsEquivalentTo(running).Should().BeTrue();

        first.Port = 1082;
        var changed = await PreparedCoreConfigBundle.CreateAsync(context, null);
        await changed.IsEquivalentTo(running).Should().BeFalse();
        first.Port = 1080;
        group.SetProtocolExtra(group.GetProtocolExtra() with { ChildItems = "second,first" });
        var reordered = await PreparedCoreConfigBundle.CreateAsync(context, null);
        await reordered.IsEquivalentTo(running).Should().BeFalse();
    }

    [Test]
    public async Task UserTemplateLogDate_IsARealConfigurationChange()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var context = CoreConfigTestFactory.CreateContext(config, CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray), ECoreType.Xray) with
        {
            FullConfigTemplate = new FullConfigTemplateItem
            {
                Enabled = true,
                CoreType = ECoreType.Xray,
                Config = "{\"log\":{\"error\":\"C:/logs/Verror_2026-10-31.txt\"},\"inbounds\":[],\"outbounds\":[]}"
            }
        };
        var running = await PreparedCoreConfigBundle.CreateAsync(context, null);
        await JsonNode.Parse(running.MainContent)!["log"]!["error"]!.ToString().Should().BeEqualTo("C:/logs/Verror_2026-10-31.txt");
        context.FullConfigTemplate.Config = context.FullConfigTemplate.Config.Replace("2026-10-31", "2026-11-01");
        var changed = await PreparedCoreConfigBundle.CreateAsync(context, null);
        await changed.IsEquivalentTo(running).Should().BeFalse();
    }

    [Test]
    public async Task OpaqueCustomChange_IsUnknownUntilAnExplicitReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opaque-{Guid.NewGuid():N}.yaml");
        try
        {
            var config = CoreConfigTestFactory.CreateConfig();
            var node = new ProfileItem { IndexId = "opaque", ConfigType = EConfigType.Custom, CoreType = ECoreType.hysteria, Address = path };
            var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.hysteria);
            await File.WriteAllTextAsync(path, "server: example.com:443\nauth: pass\n");
            var running = await PreparedCoreConfigBundle.CreateAsync(context, null);
            var same = await PreparedCoreConfigBundle.CreateAsync(context, null);
            await same.CompareTo(running).Should().BeEqualTo(ECoreConfigChange.Unchanged);
            await File.WriteAllTextAsync(path, "auth: pass\nserver: example.com:443\n");
            var reordered = await PreparedCoreConfigBundle.CreateAsync(context, null);
            await reordered.CompareTo(running).Should().BeEqualTo(ECoreConfigChange.Unknown);
        }
        finally { File.Delete(path); }
    }
}
