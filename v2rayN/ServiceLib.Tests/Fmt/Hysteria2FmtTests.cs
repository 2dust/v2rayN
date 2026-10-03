namespace ServiceLib.Tests.Fmt;

public class Hysteria2FmtTests
{
    // "The hostname and optional port of the server. If the port is omitted, it defaults to 443."
    // -- https://v2.hysteria.network/docs/developers/URI-Scheme/
    // A ':' with no digits after it is an omitted port too, per RFC 3986 'port = *DIGIT'.
    [Test]
    [Arguments("hysteria2://password@hy2.example/")]
    [Arguments("hysteria2://password@hy2.example")]
    [Arguments("hysteria2://password@hy2.example:/")]
    [Arguments("hy2://password@hy2.example/?sni=real.example")]
    public async Task ResolveConfig_WithoutPort_ShouldDefaultTo443(string shareUri)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.ConfigType.Should().BeEqualTo(EConfigType.Hysteria2);
        await resolved.Address.Should().BeEqualTo("hy2.example");
        await resolved.Port.Should().BeEqualTo(443);
    }

    [Test]
    public async Task ResolveConfig_WithoutPort_ShouldProduceAValidProfile()
    {
        // Uri.Port is -1 for an unregistered scheme with no port, and ProfileItem.IsValid rejects
        // any port outside 1..65535 - so the default is what keeps such a link usable at all.
        var resolved = FmtHandler.ResolveConfig("hysteria2://password@hy2.example/", out _);

        await resolved.Should().NotBeNull();
        await resolved!.IsValid().Should().BeTrue();
    }

    [Test]
    public async Task ResolveConfig_WithExplicitPort_ShouldKeepIt()
    {
        var resolved = FmtHandler.ResolveConfig("hysteria2://password@hy2.example:8443/", out _);

        await resolved.Should().NotBeNull();
        await resolved!.Port.Should().BeEqualTo(8443);
    }

    [Test]
    public async Task ResolveConfig_WithExplicitZeroPort_ShouldNotApplyTheDefault()
    {
        // Uri.Port is 0 here, not -1: the port is present, it is just not a usable one. Treating
        // it as "omitted" would silently move the endpoint to :443, so it stays invalid instead.
        var resolved = FmtHandler.ResolveConfig("hysteria2://password@hy2.example:0/", out _);

        await resolved.Should().NotBeNull();
        await resolved!.Port.Should().BeEqualTo(0);
        await resolved.IsValid().Should().BeFalse();
    }
}
