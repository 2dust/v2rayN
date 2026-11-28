using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

public class ConfigHandlerSubscriptionMuxTests
{
    [Test]
    [Arguments(true, 443)]
    [Arguments(false, 443)]
    [Arguments(true, 8443)]
    public async Task AddBatchServers_PreservesMuxForMatchingSubscriptionNodes(bool muxEnabled, int refreshedPort)
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());
        TestDatabase.Initialize();
        var subId = $"sub-mux-{Guid.NewGuid():N}";
        await SQLiteHelper.Instance.ReplaceAsync(new SubItem
        {
            Id = subId,
            Remarks = "subscription",
            Url = "http://127.0.0.1/sub",
        });

        await (await ConfigDbHandler.AddBatchServers(NodeUri(443), subId, true)).Should().BeEqualTo(1);
        var original = (await ConfigDbQuery.ProfileItems(subId)).Single();
        original.MuxEnabled = muxEnabled;
        await SQLiteHelper.Instance.UpdateAsync(original);

        await (await ConfigDbHandler.AddBatchServers(NodeUri(refreshedPort), subId, true)).Should().BeEqualTo(1);
        var refreshed = (await ConfigDbQuery.ProfileItems(subId)).Single();
        await refreshed.Port.Should().BeEqualTo(refreshedPort);
        bool? expectedMux = refreshedPort == 443 ? muxEnabled : null;
        await (refreshed.MuxEnabled == expectedMux).Should().BeTrue();

        static string NodeUri(int port) =>
            $"vless://00000000-0000-0000-0000-000000000001@127.0.0.1:{port}?encryption=none&security=tls&type=tcp#test-node";
    }
}
