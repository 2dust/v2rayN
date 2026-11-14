namespace ServiceLib.Tests.Handler;

public class CoreConfigHandlerTests
{
    [Test]
    public async Task GenerateClientConfig_CustomProfileWithMissingSource_ShouldIncludeSourcePath()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"v2rayN-missing-{Guid.NewGuid():N}.json");
        var outputPath = Path.Combine(Path.GetTempPath(), $"v2rayN-output-{Guid.NewGuid():N}.json");
        var node = new ProfileItem
        {
            ConfigType = EConfigType.Custom,
            Address = sourcePath,
            Remarks = "missing custom config",
        };
        var context = new CoreConfigContext
        {
            Node = node,
            RunCoreType = ECoreType.Xray,
        };

        try
        {
            var result = await CoreConfigHandler.GenerateClientConfig(context, outputPath);

            await result.Success.Should().BeFalse();
            await result.Msg.Should().Contain(ResUI.FailedReadConfiguration);
            await result.Msg.Should().Contain(sourcePath);
            await File.Exists(outputPath).Should().BeFalse();
        }
        finally
        {
            File.Delete(outputPath);
        }
    }
}
