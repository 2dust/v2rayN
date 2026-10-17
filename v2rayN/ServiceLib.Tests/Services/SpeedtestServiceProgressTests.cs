namespace ServiceLib.Tests.Services;

public class SpeedtestServiceProgressTests
{
    [Test]
    public async Task MarkCompletedAsync_ShouldReportEveryFinishedProfile()
    {
        var progress = new List<SpeedTestProgress>();
        var service = new SpeedtestService(new Config { SpeedTestItem = new() }, _ => Task.CompletedTask,
            p =>
            {
                progress.Add(p);
                return Task.CompletedTask;
            });
        SetTotal(service, 3);
        var completed = new ConcurrentDictionary<string, byte>();

        await MarkCompleted(service, completed, "a");
        await MarkCompleted(service, completed, "b");
        await MarkCompleted(service, completed, "c");

        await progress.Count.Should().BeEqualTo(3);
        await string.Join(",", progress.Select(p => p.Completed)).Should().BeEqualTo("1,2,3");
        await progress.All(p => p.Total == 3).Should().BeTrue();
        await progress.All(p => p.IsRunning).Should().BeTrue();
        await progress[^1].Percent.Should().BeEqualTo(100);
    }

    [Test]
    public async Task MarkCompletedAsync_ShouldIgnoreAlreadyCompletedProfiles()
    {
        var progress = new List<SpeedTestProgress>();
        var service = new SpeedtestService(new Config { SpeedTestItem = new() }, _ => Task.CompletedTask,
            p =>
            {
                progress.Add(p);
                return Task.CompletedTask;
            });
        SetTotal(service, 2);
        var completed = new ConcurrentDictionary<string, byte>();

        await MarkCompleted(service, completed, "a");
        await MarkCompleted(service, completed, "a");

        await progress.Count.Should().BeEqualTo(1);
        await progress[0].Completed.Should().BeEqualTo(1);
    }

    [Test]
    public async Task UpdateProgressAsync_NotRunning_ShouldClearTheIndicator()
    {
        SpeedTestProgress? last = null;
        var service = new SpeedtestService(new Config { SpeedTestItem = new() }, _ => Task.CompletedTask,
            p =>
            {
                last = p;
                return Task.CompletedTask;
            });
        SetTotal(service, 5);

        await UpdateProgress(service, 5, false);

        await last.Should().NotBeNull();
        await last!.IsRunning.Should().BeFalse();
        await last.Total.Should().BeEqualTo(5);
    }

    private static async Task MarkCompleted(SpeedtestService service, ConcurrentDictionary<string, byte> completed, string indexId)
    {
        var method = typeof(SpeedtestService).GetMethod("MarkCompletedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(service, [completed, indexId])!;
    }

    private static async Task UpdateProgress(SpeedtestService service, int completed, bool isRunning)
    {
        var method = typeof(SpeedtestService).GetMethod("UpdateProgressAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(service, [completed, isRunning])!;
    }

    private static void SetTotal(SpeedtestService service, int total)
    {
        typeof(SpeedtestService).GetField("_totalCount", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, total);
    }
}