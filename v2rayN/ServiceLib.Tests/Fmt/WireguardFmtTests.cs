namespace ServiceLib.Tests.Fmt;

public class WireguardFmtTests
{
    [Test]
    public async Task ResolveConfig_ShouldParsePeersAndIgnoreInlineComments()
    {
        const string config =
            """
            [Interface]
            PrivateKey = interface-private-key
            Address = 10.0.0.2/32, fd00::2/128 ; inline comment
            MTU = 1420
            DNS = 2001::db8::53, 1.2.3.4, 2001:db8::54

            [Peer]
            PublicKey = peer-public-key
            PresharedKey = peer-preshared-key
            Reserved = 1, 2, 3 # inline comment
            Endpoint = [2001:db8::1]:51820 # inline comment

            [Peer]
            PublicKey = peer-public-key-2
            Endpoint = example.com:12345
            """;

        var resolved = WireguardFmt.ResolveConfig(config);

        await resolved.Should().NotBeNull();
        await resolved.Should().HaveCount(2);

        var first = resolved![0];
        await first.Address.Should().BeEqualTo("2001:db8::1");
        await first.Port.Should().BeEqualTo(51820);
        await first.Password.Should().BeEqualTo("interface-private-key");
        await first.GetProtocolExtra().WgReserved.Should().BeEqualTo("1, 2, 3");
        await first.GetProtocolExtra().WgInterfaceAddress.Should().BeEqualTo("10.0.0.2/32, fd00::2/128");
        await first.GetProtocolExtra().WgMtu.Should().BeEqualTo(1420);
        await first.GetProtocolExtra().WgDns.Should().BeEqualTo("2001::db8::53, 1.2.3.4, 2001:db8::54");

        var second = resolved[1];
        await second.Address.Should().BeEqualTo("example.com");
        await second.Port.Should().BeEqualTo(12345);
    }
}
