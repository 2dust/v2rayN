namespace ServiceLib.Tests.CoreConfig.V2ray;

public class CoreConfigV2raySpeedtestTests
{
    private static ProfileItem CreateVlessNode(string indexId, string address, string streamSecurity)
    {
        var node = new ProfileItem
        {
            IndexId = indexId,
            ConfigType = EConfigType.VLESS,
            CoreType = ECoreType.Xray,
            Remarks = indexId,
            Address = address,
            Port = 2082,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = streamSecurity,
            Subid = string.Empty,
        };
        node.SetProtocolExtra(node.GetProtocolExtra() with { VlessEncryption = Global.None, });
        return node;
    }

    [Test]
    public async Task IsVlessPlaintextToPublicIp_PlaintextPublicIp_ShouldBeTrue()
    {
        var node = CreateVlessNode("bad-1", "104.18.39.218", string.Empty);

        await CoreConfigV2rayService.IsVlessPlaintextToPublicIp(node).Should().BeTrue();
    }

    [Test]
    public async Task IsVlessPlaintextToPublicIp_TlsNode_ShouldBeFalse()
    {
        var node = CreateVlessNode("good-1", "104.18.39.218", Global.StreamSecurity);
        node.Sni = "example.com";

        await CoreConfigV2rayService.IsVlessPlaintextToPublicIp(node).Should().BeFalse();
    }

    [Test]
    public async Task IsVlessPlaintextToPublicIp_PlaintextDomain_ShouldBeFalse()
    {
        // Xray allows plaintext VLESS to domains, only public IPs are prohibited.
        var node = CreateVlessNode("domain-1", "example.com", string.Empty);

        await CoreConfigV2rayService.IsVlessPlaintextToPublicIp(node).Should().BeFalse();
    }

    [Test]
    public async Task IsVlessPlaintextToPublicIp_PlaintextPrivateIp_ShouldBeFalse()
    {
        var node = CreateVlessNode("lan-1", "192.168.1.10", string.Empty);

        await CoreConfigV2rayService.IsVlessPlaintextToPublicIp(node).Should().BeFalse();
    }

    [Test]
    public async Task IsVlessPlaintextToPublicIp_NonVless_ShouldBeFalse()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);

        await CoreConfigV2rayService.IsVlessPlaintextToPublicIp(node).Should().BeFalse();
    }

    [Test]
    public async Task GenerateClientSpeedtestConfig_PlaintextVless_ShouldSkipBadNodeKeepGoodNode()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var bad = CreateVlessNode("bad-1", "104.18.39.218", string.Empty);
        var good = CreateVlessNode("good-1", "104.18.39.218", Global.StreamSecurity);
        good.Sni = "example.com";

        var dummy = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, "dummy", "dummy");
        var context = CoreConfigTestFactory.CreateContext(config, dummy, ECoreType.Xray);
        context.AllProxiesMap[bad.IndexId] = bad;
        context.AllProxiesMap[good.IndexId] = good;

        var selecteds = new List<ServerTestItem>
        {
            new()
            {
                IndexId = bad.IndexId,
                Address = bad.Address,
                Port = bad.Port,
                ConfigType = bad.ConfigType,
                Profile = bad,
                CoreType = ECoreType.Xray,
            },
            new()
            {
                IndexId = good.IndexId,
                Address = good.Address,
                Port = good.Port,
                ConfigType = good.ConfigType,
                Profile = good,
                CoreType = ECoreType.Xray,
            },
        };

        var result = new CoreConfigV2rayService(context).GenerateClientSpeedtestConfig(selecteds);

        await result.Success.Should().BeTrue();
        await selecteds.First(s => s.IndexId == "bad-1").AllowTest.Should().BeFalse();
        await selecteds.First(s => s.IndexId == "good-1").AllowTest.Should().BeTrue();

        var cfg = JsonUtils.Deserialize<V2rayConfig>(result.Data!.ToString())!;
        await cfg.Should().NotBeNull();
        // Only the good node gets an inbound + outbound; the bad one must not poison the batch.
        await cfg!.inbounds.Should().HaveCount(1);
        await cfg.outbounds.Should().HaveCount(1);
    }
}
