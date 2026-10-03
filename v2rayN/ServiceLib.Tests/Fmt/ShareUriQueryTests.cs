namespace ServiceLib.Tests.Fmt;

public class ShareUriQueryTests
{
    [Test]
    [Arguments("ob%41fs")]
    [Arguments("66%ff")]
    [Arguments("100%")]
    public async Task GetShareUriAndResolveConfig_QueryValueWithPercent_ShouldSurviveTheRoundTrip(string obfsPassword)
    {
        var source = new ProfileItem
        {
            ConfigType = EConfigType.Hysteria2,
            Remarks = "percent demo",
            Address = "hy2.example",
            Port = 8443,
            Password = "pw",
        };
        source.SetProtocolExtra(new ProtocolExtraItem { SalamanderPass = obfsPassword, });

        var uri = FmtHandler.GetShareUri(source);

        await uri.Should().NotBeNull();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {uri}, msg: {msg}");
        await resolved!.GetProtocolExtra().SalamanderPass.Should().BeEqualTo(obfsPassword);
    }

    // RFC 3986 lists '=' among the sub-delimiters a query value may carry, so only the first one
    // separates the key from the value. Splitting on every '=' discarded the pair outright.
    [Test]
    public async Task ResolveConfig_QueryValueWithUnescapedEquals_ShouldNotBeDropped()
    {
        const string shareUri = "hysteria2://pw@hy2.example:8443/?ech=AAj+DQAEAAAAAA==&sni=real.example";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.EchConfigList.Should().BeEqualTo("AAj+DQAEAAAAAA==");
        await resolved.Sni.Should().BeEqualTo("real.example");
    }

    // Canonical SIP002 percent-encodes the plugin argument, and that is what this client's own
    // exporter emits. This covers the non-canonical spelling instead: the options are a ';'
    // separated list of 'key=value' pairs, so left unescaped the value always carries '='.
    [Test]
    public async Task ResolveConfig_NonCanonicalSip002PluginWithLiteralEquals_ShouldConfigureObfs()
    {
        const string shareUri =
            "ss://YWVzLTEyOC1nY206cGFzczEyMw==@1.2.3.4:8388/?plugin=obfs-local;obfs=http;obfs-host=example.com#ss";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.ConfigType.Should().BeEqualTo(EConfigType.Shadowsocks);
        await resolved.GetTransportExtra().Host.Should().BeEqualTo("example.com");
        await resolved.GetTransportExtra().RawHeaderType.Should().BeEqualTo(Global.RawHeaderHttp);
    }

    [Test]
    public async Task ResolveConfig_EscapedQueryValue_ShouldStillDecodeExactlyOnce()
    {
        const string shareUri = "hysteria2://pw@hy2.example:8443/?ech=AAj%2BDQAEAAAAAA%3D%3D&obfs=salamander&obfs-password=a%20b";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.EchConfigList.Should().BeEqualTo("AAj+DQAEAAAAAA==");
        await resolved.GetProtocolExtra().SalamanderPass.Should().BeEqualTo("a b");
    }
}
