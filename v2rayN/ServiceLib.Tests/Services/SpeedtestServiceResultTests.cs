namespace ServiceLib.Tests.Services;

public class SpeedtestServiceResultTests
{
    [Test]
    [Arguments(ESpeedActionType.Speedtest, 0)]
    [Arguments(ESpeedActionType.Mixedtest, 0)]
    [Arguments(ESpeedActionType.Realping, 12.5)]
    public async Task StartingATest_ShouldClearTheLastSpeedOfSpeedTests(ESpeedActionType actionType, double expected)
    {
        var indexId = Guid.NewGuid().ToString();
        ProfileExManager.Instance.SetTestSpeed(indexId, 12.5m);

        await SetTestResult([new() { IndexId = indexId }], actionType, ResUI.Speedtesting);

        var profileEx = (await ProfileExManager.Instance.GetProfileExs()).First(t => t.IndexId == indexId);
        await profileEx.Speed.Should().BeEqualTo((decimal)expected);
    }

    private static Task SetTestResult(List<ServerTestItem> items, ESpeedActionType actionType, string message)
    {
        var service = new SpeedtestService(new Config { SpeedTestItem = new() }, _ => Task.CompletedTask);
        var method = typeof(SpeedtestService).GetMethod("SetTestResultAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(service, [items, actionType, message])!;
    }
}
