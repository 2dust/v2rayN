namespace ServiceLib.Tests.Fmt;

public class ShadowsocksFmtTests
{
    [Test]
    [Arguments("8388", true)]
    [Arguments("\"8388\"", true)]
    [Arguments("8388", false)]
    [Arguments("\"8388\"", false)]
    public async Task ResolveSip008_ObjectAndArrayDocuments_ShouldPreserveServerFields(string port, bool useObject)
    {
        var server = $$"""
            {
                "id": "27b8a625-4f4b-4428-9f0f-8a2317db7c79",
                "remarks": "SIP008 server",
                "server": "example.com",
                "server_port": {{port}},
                "password": "example",
                "method": "chacha20-ietf-poly1305"
            }
            """;
        var document = useObject
            ? $$"""{"version": 1, "servers": [{{server}}]}"""
            : $"[{server}]";

        var resolved = ShadowsocksFmt.ResolveSip008(document);

        await resolved.Should().NotBeNull();
        await resolved!.Should().HaveCount(1);
        await resolved[0].Remarks.Should().BeEqualTo("SIP008 server");
        await resolved[0].Address.Should().BeEqualTo("example.com");
        await resolved[0].Port.Should().BeEqualTo(8388);
        await resolved[0].Password.Should().BeEqualTo("example");
        await resolved[0].GetProtocolExtra().SsMethod.Should().BeEqualTo("chacha20-ietf-poly1305");
    }

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
