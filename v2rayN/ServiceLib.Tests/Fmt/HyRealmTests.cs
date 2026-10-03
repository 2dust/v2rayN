namespace ServiceLib.Tests.Fmt;

public class HyRealmTests
{
    [Test]
    public async Task TryParse_ShouldParseValidRealm()
    {
        var str = "realm://public@realm.hy2.io/57f9be7c-2810-4f5b-8cb9-260bc84d6c90?stun=example.stun:3478&stun=example2.stun:3478";
        var result = HyRealm.TryParse(str, out var realm);
        await result.Should().BeTrue();
        await realm.Should().NotBeNull();

        await realm.IsHttp.Should().BeFalse();
        await realm.Token.Should().BeEqualTo("public");
        await realm.RendezvousHost.Should().BeEqualTo("realm.hy2.io");
        await realm.RendezvousPort.Should().BeEqualTo(443);
        await realm.RealmName.Should().BeEqualTo("57f9be7c-2810-4f5b-8cb9-260bc84d6c90");
        await realm.StunList.Should().HaveCount(2);
        await realm.StunList.Should().Contain("example.stun:3478");
        await realm.StunList.Should().Contain("example2.stun:3478");
    }

    [Test]
    public async Task ToUri_ShouldGenerateValidUri()
    {
        var realm = new HyRealm(
            IsHttp: false,
            Token: "public",
            RendezvousHost: "realm.hy2.io",
            RendezvousPort: 443,
            RealmName: "57f9be7c-2810-4f5b-8cb9-260bc84d6c90",
            StunList: ["example.stun:3478", "example2.stun:3478"]
        );
        var uri = realm.ToUri();
        await uri.Should().Contain("realm://public@realm.hy2.io");
        await uri.Should().Contain("/57f9be7c-2810-4f5b-8cb9-260bc84d6c90");
        await uri.Should().Contain("stun=example.stun:3478");
        await uri.Should().Contain("stun=example2.stun:3478");
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Hy2Realm_ShouldRoundTripBasicFields()
    {
        var str = "hysteria2+realm://mytoken@rendezvous.example.com/my-cabin-1f3a8c2e9b?auth=your_password&insecure=1&pinSHA256=deadbeef#remark";
        var resolved = Hysteria2Fmt.ResolveRealm(str, out var msg);
        await resolved.Should().NotBeNull();
        await resolved.Password.Should().BeEqualTo("your_password");
        var result = HyRealm.TryParse(resolved.GetProtocolExtra().Hy2RealmUrl, out var realm);
        await result.Should().BeTrue();
        await realm.Should().NotBeNull();
        await realm.Token.Should().BeEqualTo("mytoken");

        // To uri
        var uri = Hysteria2Fmt.ToUri(resolved);
        await uri.Should().Contain("hysteria2+realm://mytoken@rendezvous.example.com");
        await uri.Should().EndWith("#remark");
    }

    [Test]
    public async Task ToServerUrl_ShouldIncludeSchemeForSingbox()
    {
        var realm = new HyRealm(
            IsHttp: false,
            Token: "public",
            RendezvousHost: "realm.hy2.io",
            RendezvousPort: 443,
            RealmName: "my-realm-id",
            StunList: ["turn.cloudflare.com:3478"]
        );

        await realm.ToServerUrl().Should().BeEqualTo("https://realm.hy2.io:443");
    }

    [Test]
    public async Task ResolveRealm_Issue9635_ShouldProduceHttpsServerUrl()
    {
        var str = "hysteria2+realm://public@realm.hy2.io/my-realm-id?auth=uuid&stun=turn.cloudflare.com%3A3478&sni=cloudflare.com&pinSHA256=xxx#Realm-Test";
        var resolved = Hysteria2Fmt.ResolveRealm(str, out _);
        await resolved.Should().NotBeNull();

        await HyRealm.TryParse(resolved!.GetProtocolExtra().Hy2RealmUrl, out var realm).Should().BeTrue();
        await realm!.ToServerUrl().Should().StartWith("https://");
        await realm.ToServerUrl().Should().Contain("realm.hy2.io");
        await realm.StunList.Should().Contain("turn.cloudflare.com:3478");
    }
}
