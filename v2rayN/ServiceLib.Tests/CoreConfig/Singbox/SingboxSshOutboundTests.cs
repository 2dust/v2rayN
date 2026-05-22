using AwesomeAssertions;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Models;
using ServiceLib.Services.CoreConfig;
using Xunit;

namespace ServiceLib.Tests.CoreConfig.Singbox;

public class SingboxSshOutboundTests
{
    [Fact]
    public void Ssh_PasswordOnly_EmitsExpectedFields()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSshNode(password: "p@ss");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        result.Success.Should().BeTrue($"ret msg: {result.Msg}");

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);

        proxy.type.Should().Be("ssh");
        proxy.server.Should().Be(node.Address);
        proxy.server_port.Should().Be(node.Port);
        proxy.user.Should().Be(node.Username);
        proxy.password.Should().Be("p@ss");
        proxy.private_key.Should().BeNull();
        proxy.private_key_path.Should().BeNull();
        proxy.host_key.Should().BeNull();
        proxy.multiplex.Should().BeNull();
        proxy.tls.Should().BeNull();
    }

    [Fact]
    public void Ssh_InlinePrivateKey_IsSplitIntoLines()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var pem = "-----BEGIN OPENSSH PRIVATE KEY-----\r\nAAAA\nBBBB\n\n-----END OPENSSH PRIVATE KEY-----";
        var node = CoreConfigTestFactory.CreateSshNode(password: string.Empty, privateKeyPem: pem);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        result.Success.Should().BeTrue($"ret msg: {result.Msg}");

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);

        proxy.private_key.Should().NotBeNull();
        proxy.private_key!.Should().ContainInOrder("-----BEGIN OPENSSH PRIVATE KEY-----", "AAAA", "BBBB",
            "-----END OPENSSH PRIVATE KEY-----");
        proxy.private_key.Should().NotContain(string.Empty);
    }

    [Fact]
    public void Ssh_HostKeyCsv_BecomesTrimmedList()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateSshNode(
            hostKey: " ssh-ed25519 AAAA , ssh-rsa BBBB ",
            hostKeyAlgorithms: "ssh-ed25519, rsa-sha2-256");
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        result.Success.Should().BeTrue($"ret msg: {result.Msg}");

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var proxy = cfg.outbounds.First(o => o.tag == Global.ProxyTag);

        proxy.host_key.Should().Equal("ssh-ed25519 AAAA", "ssh-rsa BBBB");
        proxy.host_key_algorithms.Should().Equal("ssh-ed25519", "rsa-sha2-256");
    }

    [Fact]
    public void Ssh_InProxyChain_ReceivesDetourTag()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var sshNode = CoreConfigTestFactory.CreateSshNode(indexId: "ssh1");
        var socksNode = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "s1", "exit");
        var chain = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.sing_box, "c1", "chain",
            [sshNode.IndexId, socksNode.IndexId]);

        var context = CoreConfigTestFactory.CreateContext(config, chain, ECoreType.sing_box);
        context.AllProxiesMap[sshNode.IndexId] = sshNode;
        context.AllProxiesMap[socksNode.IndexId] = socksNode;
        context.AllProxiesMap[chain.IndexId] = chain;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        result.Success.Should().BeTrue($"ret msg: {result.Msg}");

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        cfg.outbounds.Should().Contain(o => o.type == "ssh");
        cfg.outbounds.Where(o => o.type == "ssh")
            .Should().OnlyContain(o => !string.IsNullOrEmpty(o.detour));
    }
}
