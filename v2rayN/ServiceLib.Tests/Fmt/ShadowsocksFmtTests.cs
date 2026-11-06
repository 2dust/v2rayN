namespace ServiceLib.Tests.Fmt;

public class ShadowsocksFmtTests
{
    // SIP002 user info that is not base64 is "method:password", percent-encoded once.
    [Test]
    [Arguments("ss://2022-blake3-aes-128-gcm:p%2541ss@1.2.3.4:8388#demo", "p%41ss")]
    [Arguments("ss://2022-blake3-aes-128-gcm:p%25ss@1.2.3.4:8388#demo", "p%ss")]
    [Arguments("ss://2022-blake3-aes-128-gcm:a%3Ab%2Fc%2B%3D@1.2.3.4:8388#demo", "a:b/c+=")]
    public async Task ResolveConfig_PlainUserInfo_ShouldDecodePasswordOnce(string shareUri, string password)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {shareUri}, msg: {msg}");
        await resolved!.ConfigType.Should().BeEqualTo(EConfigType.Shadowsocks);
        await resolved.GetProtocolExtra().SsMethod.Should().BeEqualTo("2022-blake3-aes-128-gcm");
        await resolved.Password.Should().BeEqualTo(password);
    }
}
