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

    [Test]
    public async Task Preparation_DetachesMainAndPreContextsAndResetsPerGenerationState()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray) with
        {
            RawDnsItem = new DNSItem { NormalDNS = "source-dns" },
            FullConfigTemplate = new FullConfigTemplateItem
            {
                Id = "template",
                Remarks = "source-template",
                CoreType = ECoreType.Xray,
                Config = "source-config",
                TunConfig = "source-tun",
                AddProxyOnly = true,
                ProxyDetour = "source-detour"
            },
            CustomOutboundContent = new() { ["unused"] = "{}" },
            ServerTestItemMap = new() { ["test"] = "source" },
            ProtectDomainList = ["source.example"],
            ProtectCoreTypeList = [ECoreType.Xray],
            CustomOutboundMap = new() { [new object()] = "previous-generation" }
        };
        var preNode = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "pre");
        var preContext = context with
        {
            Node = preNode,
            RunCoreType = ECoreType.sing_box,
            AllProxiesMap = new() { [preNode.IndexId] = preNode }
        };

        var bundle = await PreparedCoreConfigBundle.CreateAsync(context, preContext);
        var main = GetPreparedContext(bundle, "MainContext");
        var pre = GetPreparedContext(bundle, "PreContext");
        foreach (var snapshot in new[] { main, pre })
        {
            await snapshot.CustomOutboundMap.Count.Should().BeEqualTo(0);
            await snapshot.FullConfigTemplate!.Id.Should().BeEqualTo("template");
            await snapshot.FullConfigTemplate.AddProxyOnly.Should().BeEqualTo(true);
            await snapshot.FullConfigTemplate.ProxyDetour.Should().BeEqualTo("source-detour");
            snapshot.Node.Password = "snapshot-password";
            snapshot.AppConfig.Inbound[0].DestOverride.Add("quic");
            snapshot.SimpleDnsItem.Hosts = "snapshot-host";
            snapshot.RoutingItem!.RuleSet = "[{}]";
            snapshot.RawDnsItem!.NormalDNS = "snapshot-dns";
            snapshot.FullConfigTemplate.Config = "snapshot-template";
            snapshot.AllProxiesMap.Values.First().Port = 9999;
            snapshot.CustomOutboundContent["unused"] = "changed";
            snapshot.ServerTestItemMap["test"] = "changed";
            snapshot.ProtectDomainList.Add("snapshot.example");
            snapshot.ProtectCoreTypeList.Add(ECoreType.mihomo);
            snapshot.CustomOutboundMap[new object()] = "snapshot-generation";
        }

        await node.Password.Should().BeEqualTo("pass");
        await preNode.Password.Should().BeEqualTo("pass");
        await node.Port.Should().BeEqualTo(1080);
        await preNode.Port.Should().BeEqualTo(1080);
        await config.Inbound[0].DestOverride.SequenceEqual(["http", "tls"]).Should().BeTrue();
        await config.SimpleDNSItem.Hosts.Should().BeNull();
        await context.RoutingItem!.RuleSet.Should().BeEqualTo("[]");
        await context.RawDnsItem.NormalDNS.Should().BeEqualTo("source-dns");
        await context.FullConfigTemplate.Config.Should().BeEqualTo("source-config");
        await context.CustomOutboundContent["unused"].Should().BeEqualTo("{}");
        await context.ServerTestItemMap["test"].Should().BeEqualTo("source");
        await context.ProtectDomainList.SetEquals(["source.example"]).Should().BeTrue();
        await context.ProtectCoreTypeList.SetEquals([ECoreType.Xray]).Should().BeTrue();
        await context.CustomOutboundMap.Values.Single().Should().BeEqualTo("previous-generation");
    }

    [Test]
    [Arguments("tun", ECoreConfigChange.Changed)]
    [Arguments("core", ECoreConfigChange.Changed)]
    [Arguments("arguments", ECoreConfigChange.Changed)]
    [Arguments("absolute-path", ECoreConfigChange.Changed)]
    [Arguments("environment-value", ECoreConfigChange.Changed)]
    [Arguments("environment-order", ECoreConfigChange.Unchanged)]
    [Arguments("executable-path", ECoreConfigChange.Changed)]
    [Arguments("executable-length", ECoreConfigChange.Changed)]
    [Arguments("executable-time", ECoreConfigChange.Changed)]
    public async Task IdenticalCustomContent_StillComparesCompleteLaunchIdentity(string change, ECoreConfigChange expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"v2rayn-launch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "custom.json");
        var executable = Utils.GetExeName(Path.Combine(directory, "fixture-core"));
        var otherExecutable = Utils.GetExeName(Path.Combine(directory, "other-core"));
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray)!;
        var originalExes = info.CoreExes;
        var originalArguments = info.Arguments;
        var originalAbsolutePath = info.AbsolutePath;
        var originalEnvironment = info.Environment;
        try
        {
            await File.WriteAllTextAsync(source, "{\"rules\":[\"first\",\"second\"]}");
            await File.WriteAllTextAsync(executable, "fixture");
            await File.WriteAllTextAsync(otherExecutable, "fixture");
            File.SetLastWriteTimeUtc(executable, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            info.CoreExes = [executable];
            info.Arguments = "run -c {0}";
            info.AbsolutePath = false;
            info.Environment = new Dictionary<string, string?> { ["FIRST"] = "one", ["SECOND"] = "two" };
            var config = CoreConfigTestFactory.CreateConfig();
            var node = new ProfileItem { IndexId = "custom", ConfigType = EConfigType.Custom, CoreType = ECoreType.Xray, Address = source };
            var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
            var running = await PreparedCoreConfigBundle.CreateAsync(context, null);

            switch (change)
            {
                case "tun": context = context with { IsTunEnabled = true }; break;
                case "core": context = context with { RunCoreType = ECoreType.v2fly }; break;
                case "arguments": info.Arguments = "run -c {0} --changed"; break;
                case "absolute-path": info.AbsolutePath = true; break;
                case "environment-value": info.Environment["FIRST"] = "changed"; break;
                case "environment-order": info.Environment = new Dictionary<string, string?> { ["SECOND"] = "two", ["FIRST"] = "one" }; break;
                case "executable-path": info.CoreExes = [otherExecutable]; break;
                case "executable-length": await File.AppendAllTextAsync(executable, "-changed"); break;
                case "executable-time": File.SetLastWriteTimeUtc(executable, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)); break;
            }

            var candidate = await PreparedCoreConfigBundle.CreateAsync(context, null);
            await candidate.MainContent.Should().BeEqualTo(running.MainContent);
            await candidate.CompareTo(running).Should().BeEqualTo(expected);
        }
        finally
        {
            info.CoreExes = originalExes;
            info.Arguments = originalArguments;
            info.AbsolutePath = originalAbsolutePath;
            info.Environment = originalEnvironment;
            Directory.Delete(directory, true);
        }
    }

    private static CoreConfigContext GetPreparedContext(PreparedCoreConfigBundle bundle, string property) =>
        (CoreConfigContext)typeof(PreparedCoreConfigBundle)
            .GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bundle)!;
}
