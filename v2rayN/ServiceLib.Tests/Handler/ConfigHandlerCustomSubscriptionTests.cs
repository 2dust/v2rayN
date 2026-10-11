using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

public class ConfigHandlerCustomSubscriptionTests
{
    private const string FullXrayConfigs = """
        [
          {"remarks":"custom-1","inbounds":[{"tag":"socks","listen":"127.0.0.1","port":10808,"protocol":"socks","settings":{"auth":"noauth","udp":true}}],"outbounds":[{"tag":"proxy","protocol":"vless","settings":{"vnext":[{"address":"127.0.0.1","port":443,"users":[{"id":"00000000-0000-0000-0000-000000000001","encryption":"none"}]}]},"streamSettings":{"network":"tcp"}}],"routing":{"domainStrategy":"AsIs","rules":[]}},
          {"remarks":"custom-2","inbounds":[{"tag":"socks","listen":"127.0.0.1","port":10808,"protocol":"socks","settings":{"auth":"noauth","udp":true}}],"outbounds":[{"tag":"proxy","protocol":"vless","settings":{"vnext":[{"address":"127.0.0.1","port":443,"users":[{"id":"00000000-0000-0000-0000-000000000001","encryption":"none"}]}]},"streamSettings":{"network":"tcp"}}],"routing":{"domainStrategy":"AsIs","rules":[]}}
        ]
        """;

    [Test]
    public async Task AddBatchServers_FullConfigsFromSubscription_InheritSubscriptionPreSocksPort()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();

        var subId = $"sub-{Guid.NewGuid():N}";
        await SQLiteHelper.Instance.ReplaceAsync(new SubItem
        {
            Id = subId,
            Remarks = "sub",
            Url = "http://127.0.0.1/sub",
            PreSocksPort = 10808,
        });

        var count = await ConfigHandler.AddBatchServers(config, FullXrayConfigs, subId, true);
        var profiles = await AppManager.Instance.ProfileItems(subId) ?? [];

        await count.Should().BeEqualTo(2);
        await profiles.Should().HaveCount(2);
        foreach (var profile in profiles)
        {
            await profile.ConfigType.Should().BeEqualTo(EConfigType.Custom);
            await profile.PreSocksPort.Should().BeEqualTo(10808);
        }
    }
}
