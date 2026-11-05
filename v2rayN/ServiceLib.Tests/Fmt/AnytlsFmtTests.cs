namespace ServiceLib.Tests.Fmt;

public class AnytlsFmtTests
{
    // "服务器的地址和可选端口。如果省略端口，则默认为 443。" (the port is optional and defaults to 443)
    // -- https://github.com/anytls/anytls-go/blob/main/docs/uri_scheme.md
    // The first two cases are the examples given there.
    [Test]
    [Arguments("anytls://letmein@example.com/?sni=real.example.com")]
    [Arguments("anytls://letmein@example.com/?sni=127.0.0.1&insecure=1")]
    [Arguments("anytls://letmein@example.com")]
    [Arguments("anytls://letmein@example.com:/")]
    public async Task ResolveConfig_WithoutPort_ShouldDefaultTo443(string shareUri)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.ConfigType.Should().BeEqualTo(EConfigType.Anytls);
        await resolved.Address.Should().BeEqualTo("example.com");
        await resolved.Port.Should().BeEqualTo(443);
        await resolved.Password.Should().BeEqualTo("letmein");
        await resolved.IsValid().Should().BeTrue();
    }

    [Test]
    public async Task ResolveConfig_WithExplicitPort_ShouldKeepIt()
    {
        // The third example from the URI scheme document.
        const string shareUri = "anytls://0fdf77d7-d4ba-455e-9ed9-a98dd6d5489a@[2409:8a71:6a00:1953::615]:8964/?insecure=1";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.Address.Should().BeEqualTo("2409:8a71:6a00:1953::615");
        await resolved.Port.Should().BeEqualTo(8964);
    }

    [Test]
    public async Task ResolveConfig_WithExplicitZeroPort_ShouldNotApplyTheDefault()
    {
        var resolved = FmtHandler.ResolveConfig("anytls://letmein@example.com:0/", out _);

        await resolved.Should().NotBeNull();
        await resolved!.Port.Should().BeEqualTo(0);
        await resolved.IsValid().Should().BeFalse();
    }
}
