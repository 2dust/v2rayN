using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

[NotInParallel]
public class ConfigHandlerSubscriptionStatisticsTests
{
    private const string Uuid1 = "00000000-0000-4000-8000-000000000001";
    private const string Uuid2 = "00000000-0000-4000-8000-000000000002";

    [Test]
    public async Task AddBatchServers_RefreshedSubscription_KeepsTrafficStatisticsOfMatchedServers()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        TestDatabase.Initialize();
        SQLiteHelper.Instance.CreateTable<ServerStatItem>();
        await StatisticsManager.Instance.ClearAllServerStatistics();

        var subId = $"sub-stat-{Guid.NewGuid():N}";
        await SQLiteHelper.Instance.ReplaceAsync(new SubItem { Id = subId, Remarks = "sub", Url = "http://127.0.0.1/sub" });

        try
        {
            await ConfigDbHandler.AddBatchServers(Links(("a.example.com", 443, Uuid1, "a"), ("b.example.com", 443, Uuid2, "b")), subId, true);
            var oldProfiles = await ConfigDbQuery.ProfileItems(subId) ?? [];
            StatisticsManager.Instance.ServerStat.Add(new ServerStatItem { IndexId = oldProfiles.First(t => t.Remarks == "a").IndexId, TotalUp = 1 });
            StatisticsManager.Instance.ServerStat.Add(new ServerStatItem { IndexId = oldProfiles.First(t => t.Remarks == "b").IndexId, TotalUp = 2 });

            // "a" is unchanged, "b" moved to another port, "c" is a new name on b's address.
            await ConfigDbHandler.AddBatchServers(Links(("a.example.com", 443, Uuid1, "a"), ("b.example.com", 8443, Uuid2, "b"), ("b.example.com", 2053, Uuid1, "c")), subId, true);
            var newProfiles = await ConfigDbQuery.ProfileItems(subId) ?? [];

            await newProfiles.Should().HaveCount(3);
            await TotalUp(newProfiles, "a").Should().BeEqualTo(1L);
            await TotalUp(newProfiles, "b").Should().BeEqualTo(2L);
            await TotalUp(newProfiles, "c").Should().BeEqualTo(2L);
        }
        finally
        {
            await StatisticsManager.Instance.ClearAllServerStatistics();
            await ConfigDbHandler.RemoveServersViaSubid(subId, false);
        }
    }

    private static string Links(params (string Address, int Port, string Uuid, string Remarks)[] servers)
    {
        return string.Join("\n", servers.Select(s =>
            $"vless://{s.Uuid}@{s.Address}:{s.Port}?encryption=none&security=tls&sni={s.Address}&type=tcp#{s.Remarks}"));
    }

    private static long? TotalUp(List<ProfileItem> profiles, string remarks)
    {
        var indexId = profiles.First(t => t.Remarks == remarks).IndexId;
        return StatisticsManager.Instance.ServerStat.FirstOrDefault(t => t.IndexId == indexId)?.TotalUp;
    }
}
