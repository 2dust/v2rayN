namespace ServiceLib.Tests.Fmt;

public class SocksFmtTests
{
    // The legacy form base64-encodes the whole "user:pass@host:port".
    [Test]
    [Arguments("socks://YWRtaW46YWRtaW5AMS4yLjMuNDoxMDgw#demo", "admin", "admin", "1.2.3.4", 1080)]
    [Arguments("socks://dXNlcjpwYXNzQDEuMi4zLjQ6MTA4MA==#demo", "user", "pass", "1.2.3.4", 1080)]
    [Arguments("socks://dXNlcjpxd2VydHk/QDEuMi4zLjQ6MTA4MA==#demo", "user", "qwerty?", "1.2.3.4", 1080)]
    [Arguments("socks://dXNlcjpxd2VydHk_QDEuMi4zLjQ6MTA4MA#demo", "user", "qwerty?", "1.2.3.4", 1080)]
    public async Task ResolveConfig_LegacyBase64_ShouldDecodeServerAndCredentials(
        string shareUri, string username, string password, string address, int port)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.ConfigType.Should().BeEqualTo(EConfigType.SOCKS);
        await resolved.Address.Should().BeEqualTo(address);
        await resolved.Port.Should().BeEqualTo(port);
        await resolved.Username.Should().BeEqualTo(username);
        await resolved.Password.Should().BeEqualTo(password);
        await resolved.Remarks.Should().BeEqualTo("demo");
    }

    [Test]
    [Arguments("socks://YWRtaW46YWRtaW4@1.2.3.4:1080#demo", "admin", "admin")]
    [Arguments("socks://admin:admin@1.2.3.4:1080#demo", "admin", "admin")]
    [Arguments("socks://1.2.3.4:1080#demo", "", "")]
    public async Task ResolveConfig_HostAndPort_ShouldKeepParsingIt(string shareUri, string username, string password)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.Address.Should().BeEqualTo("1.2.3.4");
        await resolved.Port.Should().BeEqualTo(1080);
        await resolved.Username.Should().BeEqualTo(username);
        await resolved.Password.Should().BeEqualTo(password);
        await resolved.Remarks.Should().BeEqualTo("demo");
    }
}
