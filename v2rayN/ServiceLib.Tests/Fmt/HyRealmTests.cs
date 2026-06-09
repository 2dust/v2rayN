using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class HyRealmTests
{
    [Fact]
    public void TryParse_ShouldParseValidRealm()
    {
        var str = "realm://public@realm.hy2.io/57f9be7c-2810-4f5b-8cb9-260bc84d6c90?stun=example.stun:3478&stun=example2.stun:3478";
        var result = HyRealm.TryParse(str, out var realm);
        result.Should().BeTrue();
        realm.Should().NotBeNull();

        realm.IsHttp.Should().BeFalse();
        realm.Token.Should().Be("public");
        realm.RendezvousHost.Should().Be("realm.hy2.io");
        realm.RendezvousPort.Should().Be(443);
        realm.RealmName.Should().Be("57f9be7c-2810-4f5b-8cb9-260bc84d6c90");
        realm.StunList.Should().HaveCount(2);
        realm.StunList.Should().Contain("example.stun:3478");
        realm.StunList.Should().Contain("example2.stun:3478");
    }

    [Fact]
    public void ToUri_ShouldGenerateValidUri()
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
        uri.Should().Contain("realm://public@realm.hy2.io");
        uri.Should().Contain("/57f9be7c-2810-4f5b-8cb9-260bc84d6c90");
        uri.Should().Contain("stun=example.stun:3478");
        uri.Should().Contain("stun=example2.stun:3478");
    }

    [Fact]
    public void GetShareUriAndResolveConfig_Hy2Realm_ShouldRoundTripBasicFields()
    {
        var str = "hysteria2+realm://mytoken@rendezvous.example.com/my-cabin-1f3a8c2e9b?auth=your_password&insecure=1&pinSHA256=deadbeef#remark";
        var resolved = Hysteria2Fmt.ResolveRealm(str, out var msg);
        resolved.Should().NotBeNull();
        resolved.Password.Should().Be("your_password");
        var result = HyRealm.TryParse(resolved.GetProtocolExtra().Hy2RealmUrl, out var realm);
        result.Should().BeTrue();
        realm.Should().NotBeNull();
        realm.Token.Should().Be("mytoken");

        // To uri
        var uri = Hysteria2Fmt.ToUri(resolved);
        uri.Should().Contain("hysteria2+realm://mytoken@rendezvous.example.com");
        uri.Should().EndWith("#remark");
    }
}
