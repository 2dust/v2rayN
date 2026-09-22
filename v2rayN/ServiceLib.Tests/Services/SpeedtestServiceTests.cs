namespace ServiceLib.Tests.Services;

public class SpeedtestServiceTests
{
    [Test]
    public async Task TcpingWithNoTestableProfilesCompletes()
    {
        var config = new Config
        {
            SpeedTestItem = new SpeedTestItem { SpeedTestPageSize = 10 },
        };
        var results = new List<SpeedTestResult>();
        var service = new SpeedtestService(config, result =>
        {
            results.Add(result);
            return Task.CompletedTask;
        });

        await service.RunLoop(ESpeedActionType.Tcping,
        [
            new ProfileItem
            {
                IndexId = "custom-tcping",
                ConfigType = EConfigType.Custom,
                CoreType = ECoreType.Xray,
            },
        ]);

        var result = results.Single(it => it.IndexId == "custom-tcping");
        await result.Delay.Should().BeEqualTo(ResUI.SpeedtestingSkip);
        await result.Speed.Should().Contain("not available");
    }
}
