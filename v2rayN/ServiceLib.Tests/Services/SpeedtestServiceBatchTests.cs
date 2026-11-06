namespace ServiceLib.Tests.Services;

public class SpeedtestServiceBatchTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1000)]
    public async Task EmptySelection_ShouldReturnNoBatches(int pageSize)
    {
        await GetBatches([], pageSize).Count.Should().BeEqualTo(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task NonPositivePageSize_ShouldKeepEveryEligibleProfile(int pageSize)
    {
        var profiles = CreateProfiles();
        var batches = GetBatches(profiles, pageSize);

        await batches.Count.Should().BeEqualTo(profiles.Count);
        await batches.SelectMany(batch => batch).Count().Should().BeEqualTo(profiles.Count);
        foreach (var profile in profiles)
        {
            await batches.SelectMany(batch => batch).Count(item => ReferenceEquals(item, profile)).Should().BeEqualTo(1);
        }
    }

    [Test]
    public async Task NormalBatches_ShouldRespectPageSizeAndKeepCoreTypesSeparate()
    {
        var profiles = CreateProfiles();
        var batches = GetBatches(profiles, 2);

        await batches.Count.Should().BeEqualTo(3);
        await batches.SelectMany(batch => batch).Count().Should().BeEqualTo(profiles.Count);
        foreach (var batch in batches)
        {
            await (batch.Count > 0 && batch.Count <= 2).Should().BeTrue();
            await batch.Select(item => item.CoreType).Distinct().Count().Should().BeEqualTo(1);
        }
    }

    private static List<ServerTestItem> CreateProfiles() =>
    [
        new() { CoreType = ECoreType.Xray },
        new() { CoreType = ECoreType.Xray },
        new() { CoreType = ECoreType.Xray },
        new() { CoreType = ECoreType.sing_box },
        new() { CoreType = ECoreType.sing_box }
    ];

    private static List<List<ServerTestItem>> GetBatches(List<ServerTestItem> profiles, int pageSize)
    {
        var service = new SpeedtestService(new Config { SpeedTestItem = new() }, _ => Task.CompletedTask);
        var method = typeof(SpeedtestService).GetMethod("GetTestBatchItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<List<ServerTestItem>>)method.Invoke(service, [profiles, pageSize])!;
    }
}
