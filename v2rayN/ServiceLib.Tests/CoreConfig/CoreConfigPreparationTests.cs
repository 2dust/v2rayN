namespace ServiceLib.Tests.CoreConfig;

[NotInParallel]
public class CoreConfigPreparationTests
{
    [Test]
    public async Task MihomoPreparation_PreservesRealityShortIdAsStringWithoutALiveFile()
    {
        var source = Path.Combine(Path.GetTempPath(), $"mihomo-prepare-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(source, "proxies:\n  - name: demo\n    type: vless\n    server: example.com\n    port: 443\n    uuid: 00000000-0000-0000-0000-000000000001\n    reality-opts:\n      public-key: example\n      short-id: 815458e4\n");
        try
        {
            var config = CoreConfigTestFactory.CreateConfig();
            CoreConfigTestFactory.BindAppManagerConfig(config);
            var node = new ProfileItem { IndexId = "mihomo", ConfigType = EConfigType.Custom, CoreType = ECoreType.mihomo, Address = source };
            var result = await CoreConfigHandler.GenerateClientConfig(CoreConfigTestFactory.CreateContext(config, node, ECoreType.mihomo), null);
            await result.Success.Should().BeTrue();
            var root = YamlUtils.FromYaml(result.Data!.ToString()!);
            var proxies = (YamlDotNet.RepresentationModel.YamlSequenceNode)root.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("proxies")];
            var proxy = (YamlDotNet.RepresentationModel.YamlMappingNode)proxies.Children[0];
            var reality = (YamlDotNet.RepresentationModel.YamlMappingNode)proxy.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("reality-opts")];
            var shortId = (YamlDotNet.RepresentationModel.YamlScalarNode)reality.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("short-id")];
            await shortId.Value.Should().BeEqualTo("815458e4");
            await shortId.Style.Should().BeEqualTo(YamlDotNet.Core.ScalarStyle.DoubleQuoted);
        }
        finally { File.Delete(source); }
    }

    [Test]
    public async Task CustomConfig_CanBePreparedWithoutWritingALiveFile()
    {
        var source = Path.Combine(Path.GetTempPath(), $"v2rayn-prepare-{Guid.NewGuid():N}.json");
        const string content = "{\"inbounds\":[],\"outbounds\":[{\"protocol\":\"freedom\"}]}";
        await File.WriteAllTextAsync(source, content);
        try
        {
            var config = CoreConfigTestFactory.CreateConfig();
            var node = new ProfileItem { IndexId = "custom", ConfigType = EConfigType.Custom, CoreType = ECoreType.Xray, Address = source };
            var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
            var result = await CoreConfigHandler.GenerateClientConfig(context, null);

            await result.Success.Should().BeTrue();
            await (result.Data?.ToString()).Should().BeEqualTo(content);
            await (await File.ReadAllTextAsync(source)).Should().BeEqualTo(content);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Test]
    public async Task CustomConfig_MissingSourceDoesNotDeleteTheExistingDestination()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"v2rayn-live-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(destination, "running configuration");
        try
        {
            var config = CoreConfigTestFactory.CreateConfig();
            var node = new ProfileItem { IndexId = "custom", ConfigType = EConfigType.Custom, CoreType = ECoreType.Xray, Address = $"missing-{Guid.NewGuid():N}.json" };
            var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray);
            var result = await CoreConfigHandler.GenerateClientConfig(context, destination);

            await result.Success.Should().BeFalse();
            await File.Exists(destination).Should().BeTrue();
            await (await File.ReadAllTextAsync(destination)).Should().BeEqualTo("running configuration");
        }
        finally
        {
            File.Delete(destination);
        }
    }
}
