namespace ServiceLib.Tests.Fmt;

public class NaiveFmtTests
{
    // "port can be omitted if it's 443" -- https://gist.github.com/DuckSoft/ca03913b0a26fc77a1da4d01cc6ab2f1
    // The first two cases are examples given there; none of its examples carries a port.
    [Test]
    [Arguments("naive+https://what:happened@test.someone.cf?padding=false#Naive!", false)]
    [Arguments("naive+quic://manhole:114514@quic.test.me", true)]
    [Arguments("naive+https://what:happened@test.someone.cf/", false)]
    [Arguments("naive+https://what:happened@test.someone.cf:/", false)]
    public async Task ResolveConfig_WithoutPort_ShouldDefaultTo443(string shareUri, bool quic)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.ConfigType.Should().BeEqualTo(EConfigType.Naive);
        await resolved.Port.Should().BeEqualTo(443);
        await (resolved.GetProtocolExtra().NaiveQuic == true).Should().BeEqualTo(quic);
        await resolved.IsValid().Should().BeTrue();
    }

    [Test]
    public async Task ResolveConfig_WithExplicitPort_ShouldKeepIt()
    {
        var resolved = FmtHandler.ResolveConfig("naive+https://what:happened@test.someone.cf:8443", out _);

        await resolved.Should().NotBeNull();
        await resolved!.Address.Should().BeEqualTo("test.someone.cf");
        await resolved.Port.Should().BeEqualTo(8443);
        await resolved.Username.Should().BeEqualTo("what");
        await resolved.Password.Should().BeEqualTo("happened");
    }

    [Test]
    public async Task ResolveConfig_WithExplicitZeroPort_ShouldNotApplyTheDefault()
    {
        var resolved = FmtHandler.ResolveConfig("naive+https://what:happened@test.someone.cf:0", out _);

        await resolved.Should().NotBeNull();
        await resolved!.Port.Should().BeEqualTo(0);
        await resolved.IsValid().Should().BeFalse();
    }
}
