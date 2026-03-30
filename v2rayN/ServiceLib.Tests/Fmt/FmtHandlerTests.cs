using AwesomeAssertions;
using ServiceLib.Handler.Fmt;
using ServiceLib.Models;
using ServiceLib.Enums;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class FmtHandlerTests
{
    [Fact]
    public void GetShareUriAndResolveConfig_Vmess_ShouldRoundTripBasicFields()
    {
        var source = CreateVmessProfile();

        var resolved = ExportThenImport(source);

        resolved.ConfigType.Should().Be(EConfigType.VMess);
        resolved.Remarks.Should().Be(source.Remarks);
        resolved.Address.Should().Be(source.Address);
        resolved.Port.Should().Be(source.Port);
        resolved.Password.Should().Be(source.Password);
        resolved.GetProtocolExtra().AlterId.Should().Be(source.GetProtocolExtra().AlterId);
    }

    [Fact]
    public void GetShareUriAndResolveConfig_Vless_ShouldRoundTripBasicFields()
    {
        var source = CreateVlessProfile();

        var resolved = ExportThenImport(source);

        resolved.ConfigType.Should().Be(EConfigType.VLESS);
        resolved.Remarks.Should().Be(source.Remarks);
        resolved.Address.Should().Be(source.Address);
        resolved.Port.Should().Be(source.Port);
        resolved.Password.Should().Be(source.Password);
        resolved.GetProtocolExtra().VlessEncryption.Should().Be(Global.None);
    }

    [Fact]
    public void GetShareUriAndResolveConfig_Shadowsocks_ShouldRoundTripBasicFields()
    {
        var source = CreateShadowsocksProfile();

        var resolved = ExportThenImport(source);

        resolved.ConfigType.Should().Be(EConfigType.Shadowsocks);
        resolved.Remarks.Should().Be(source.Remarks);
        resolved.Address.Should().Be(source.Address);
        resolved.Port.Should().Be(source.Port);
        resolved.Password.Should().Be(source.Password);
        resolved.GetProtocolExtra().SsMethod.Should().Be(source.GetProtocolExtra().SsMethod);
    }

    [Fact]
    public void GetShareUriAndResolveConfig_Socks_ShouldRoundTripBasicFields()
    {
        var source = CreateSocksProfile();

        var resolved = ExportThenImport(source);

        resolved.ConfigType.Should().Be(EConfigType.SOCKS);
        resolved.Remarks.Should().Be(source.Remarks);
        resolved.Address.Should().Be(source.Address);
        resolved.Port.Should().Be(source.Port);
        resolved.Username.Should().Be(source.Username);
        resolved.Password.Should().Be(source.Password);
    }

    [Fact]
    public void ResolveConfig_UnsupportedProtocol_ShouldReturnNull()
    {
        var resolved = FmtHandler.ResolveConfig("not-a-share-uri", out var msg);

        resolved.Should().BeNull();
        msg.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetShareUri_UnsupportedConfigType_ShouldReturnNull()
    {
        var item = new ProfileItem { ConfigType = EConfigType.PolicyGroup, Remarks = "group", };

        var uri = FmtHandler.GetShareUri(item);

        uri.Should().BeNull();
    }

    private static ProfileItem ExportThenImport(ProfileItem source)
    {
        var uri = FmtHandler.GetShareUri(source);

        uri.Should().NotBeNullOrWhiteSpace();
        (uri!.StartsWith(Global.ProtocolShares[source.ConfigType], StringComparison.OrdinalIgnoreCase)).Should()
            .BeTrue();

        var resolved = FmtHandler.ResolveConfig(uri, out var msg);

        resolved.Should().NotBeNull($"uri: {uri}, msg: {msg}");
        return resolved!;
    }

    private static ProfileItem CreateVmessProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VMess,
            Remarks = "vmess demo",
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { AlterId = "0", VmessSecurity = Global.DefaultSecurity, });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateVlessProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VLESS,
            Remarks = "vless demo",
            Address = "vless.example",
            Port = 8443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { VlessEncryption = Global.None, });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateShadowsocksProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Shadowsocks,
            Remarks = "ss demo",
            Address = "1.2.3.4",
            Port = 8388,
            Password = "pass123",
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { SsMethod = "aes-128-gcm", });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateSocksProfile()
    {
        return new ProfileItem
        {
            ConfigType = EConfigType.SOCKS,
            Remarks = "socks demo",
            Address = "127.0.0.1",
            Port = 1080,
            Username = "user",
            Password = "pass",
        };
    }
}
