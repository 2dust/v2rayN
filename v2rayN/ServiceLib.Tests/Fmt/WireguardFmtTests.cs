using AwesomeAssertions;
using ServiceLib.Handler.Fmt;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class WireguardFmtTests
{
    [Fact]
    public void ResolveConfig_ShouldParsePeersAndIgnoreInlineComments()
    {
        const string config =
            """
            [Interface]
            PrivateKey = interface-private-key
            Address = 10.0.0.2/32, fd00::2/128 ; inline comment
            MTU = 1420

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

        resolved.Should().NotBeNull();
        resolved.Should().HaveCount(2);

        var first = resolved![0];
        first.Address.Should().Be("2001:db8::1");
        first.Port.Should().Be(51820);
        first.Password.Should().Be("interface-private-key");
        first.GetProtocolExtra().WgReserved.Should().Be("1, 2, 3");
        first.GetProtocolExtra().WgInterfaceAddress.Should().Be("10.0.0.2/32, fd00::2/128");
        first.GetProtocolExtra().WgMtu.Should().Be(1420);

        var second = resolved[1];
        second.Address.Should().Be("example.com");
        second.Port.Should().Be(12345);
    }
}
