using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

public class ConfigHandlerPreResolverTests
{
    private const string Uuid = "00000000-0000-0000-0000-000000000001";

    [Test]
    public async Task AddBatchServers_PreResolvedTlsWs_KeepsDomainForSniAndHost()
    {
        var profile = await ImportWithPreResolver(
            $"vless://{Uuid}@localhost:443?encryption=none&security=tls&type=ws&path=%2Fws#tls-ws");

        await profile.Sni.Should().BeEqualTo("localhost");
        await profile.GetTransportExtra().Host.Should().BeEqualTo("localhost");
    }

    [Test]
    public async Task AddBatchServers_PreResolvedImplicitTls_KeepsDomainForSni()
    {
        var profile = await ImportWithPreResolver("hysteria2://password@localhost:443#hy2");

        await profile.Sni.Should().BeEqualTo("localhost");
    }

    [Test]
    public async Task AddBatchServers_PreResolvedTlsWsWithHost_KeepsHostAsSniFallback()
    {
        var profile = await ImportWithPreResolver(
            $"vless://{Uuid}@localhost:443?encryption=none&security=tls&type=ws&host=cdn.example.com&path=%2Fws#tls-ws-host");

        // The core derives SNI from the transport host when SNI is empty, so neither is rewritten.
        await profile.Sni.IsNullOrEmpty().Should().BeTrue();
        await profile.GetTransportExtra().Host.Should().BeEqualTo("cdn.example.com");
    }

    [Test]
    public async Task AddBatchServers_PreResolvedPlainWs_KeepsDomainForHost()
    {
        var profile = await ImportWithPreResolver(
            $"vless://{Uuid}@localhost:80?encryption=none&type=ws&path=%2Fws#plain-ws");

        await profile.Sni.IsNullOrEmpty().Should().BeTrue();
        await profile.GetTransportExtra().Host.Should().BeEqualTo("localhost");
    }

    private static async Task<ProfileItem> ImportWithPreResolver(string shareLink)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        TestDatabase.Initialize();

        var subId = $"sub-pre-resolve-{Guid.NewGuid():N}";
        await SQLiteHelper.Instance.ReplaceAsync(new SubItem
        {
            Id = subId,
            Remarks = "sub",
            Url = "http://127.0.0.1/sub",
            // Resolved by the operating system, so the test needs no network access.
            PreResolver = "localhost",
        });

        var count = await ConfigDbHandler.AddBatchServers(shareLink, subId, true);
        var profiles = await ConfigDbQuery.ProfileItems(subId) ?? [];

        await count.Should().BeEqualTo(1);
        await profiles.Should().HaveCount(1);
        var profile = profiles[0];
        await Utils.IsIpAddress(profile.Address).Should().BeTrue();
        return profile;
    }
}
