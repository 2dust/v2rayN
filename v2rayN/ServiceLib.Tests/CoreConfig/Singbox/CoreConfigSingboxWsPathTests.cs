namespace ServiceLib.Tests.CoreConfig.Singbox;

public class CoreConfigSingboxWsPathTests
{
    private static ProfileItem CreateVlessWsNode(string indexId, string path)
    {
        var node = new ProfileItem
        {
            IndexId = indexId,
            ConfigType = EConfigType.VLESS,
            CoreType = ECoreType.sing_box,
            Remarks = indexId,
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.ws),
            StreamSecurity = Global.StreamSecurity,
            Sni = "example.com",
            Subid = string.Empty,
        };
        node.SetTransportExtra(node.GetTransportExtra() with
        {
            Host = "example.com",
            Path = path,
        });
        return node;
    }

    private static async Task<Outbound4Sbox> GenProxyOutbound(ProfileItem node)
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        await cfg.Should().NotBeNull();
        var proxyOutbound = cfg!.outbounds.FirstOrDefault(o => o.tag == Global.ProxyTag);
        await proxyOutbound.Should().NotBeNull();
        return proxyOutbound!;
    }

    [Test]
    public async Task WsTransport_EncodedPathWithEd_ShouldDecodeAndExtractEarlyData()
    {
        // Subscription stores "%2F%3Fed%3D2048"; sing-box must see path "/"
        // plus max_early_data instead of the literal encoded string (#10181).
        var proxyOutbound = await GenProxyOutbound(CreateVlessWsNode("ws-enc", "%2F%3Fed%3D2048"));

        await proxyOutbound.transport.Should().NotBeNull();
        await proxyOutbound.transport!.path.Should().BeEqualTo("/");
        await proxyOutbound.transport.max_early_data.Should().BeEqualTo(2048);
        await proxyOutbound.transport.early_data_header_name.Should().BeEqualTo("Sec-WebSocket-Protocol");
    }

    [Test]
    public async Task WsTransport_DecodedPathWithEd_ShouldKeepWorking()
    {
        var proxyOutbound = await GenProxyOutbound(CreateVlessWsNode("ws-dec", "/?ed=2048"));

        await proxyOutbound.transport.Should().NotBeNull();
        await proxyOutbound.transport!.path.Should().BeEqualTo("/");
        await proxyOutbound.transport.max_early_data.Should().BeEqualTo(2048);
        await proxyOutbound.transport.early_data_header_name.Should().BeEqualTo("Sec-WebSocket-Protocol");
    }

    [Test]
    public async Task WsTransport_PlainPath_ShouldPassThrough()
    {
        var proxyOutbound = await GenProxyOutbound(CreateVlessWsNode("ws-plain", "/mypath"));

        await proxyOutbound.transport.Should().NotBeNull();
        await proxyOutbound.transport!.path.Should().BeEqualTo("/mypath");
        await proxyOutbound.transport.max_early_data.Should().BeNull();
    }
}
