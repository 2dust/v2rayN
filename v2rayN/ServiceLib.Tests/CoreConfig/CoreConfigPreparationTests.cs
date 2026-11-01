namespace ServiceLib.Tests.CoreConfig;

[NotInParallel]
public class CoreConfigPreparationTests
{
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
