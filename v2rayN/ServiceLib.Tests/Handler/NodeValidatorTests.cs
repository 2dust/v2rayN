namespace ServiceLib.Tests.Handler;

public class NodeValidatorTests
{
    [Test]
    [Arguments(nameof(ETransport.kcp))]
    [Arguments(nameof(ETransport.xhttp))]
    [Arguments(nameof(ETransport.xdrive))]
    public async Task Validate_SingboxWithXrayOnlyTransport_ShouldFail(string network)
    {
        var result = NodeValidator.Validate(CreateVlessNode(network), ECoreType.sing_box);

        await result.Success.Should().BeFalse();
    }

    [Test]
    [Arguments(nameof(ETransport.ws))]
    [Arguments(nameof(ETransport.xdrive))]
    public async Task Validate_SupportedTransport_ShouldPass(string network)
    {
        var coreType = network == nameof(ETransport.xdrive) ? ECoreType.Xray : ECoreType.sing_box;

        var result = NodeValidator.Validate(CreateVlessNode(network), coreType);

        await result.Success.Should().BeTrue();
    }

    private static ProfileItem CreateVlessNode(string network)
    {
        var node = new ProfileItem
        {
            IndexId = $"vless-{network}",
            ConfigType = EConfigType.VLESS,
            Remarks = network,
            Address = "example.com",
            Port = 443,
            Password = "01234567-89ab-cdef-0123-456789abcdef",
            Network = network,
        };
        node.SetProtocolExtra(new ProtocolExtraItem { VlessEncryption = Global.None });
        return node;
    }
}
