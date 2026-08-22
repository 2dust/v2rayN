namespace ServiceLib.Tests.Fmt;

public class FmtHandlerTests
{
    [Test]
    public async Task GetShareUriAndResolveConfig_Vmess_ShouldRoundTripBasicFields()
    {
        var source = CreateVmessProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.VMess);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().AlterId.Should().BeEqualTo(source.GetProtocolExtra().AlterId);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Vless_ShouldRoundTripBasicFields()
    {
        var source = CreateVlessProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.VLESS);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().VlessEncryption.Should().BeEqualTo(Global.None);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Shadowsocks_ShouldRoundTripBasicFields()
    {
        var source = CreateShadowsocksProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.Shadowsocks);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().SsMethod.Should().BeEqualTo(source.GetProtocolExtra().SsMethod);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Socks_ShouldRoundTripBasicFields()
    {
        var source = CreateSocksProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.SOCKS);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Trojan_ShouldRoundTripBasicFields()
    {
        var source = CreateTrojanProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.Trojan);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Sni.Should().BeEqualTo(source.Sni);
        await resolved.GetProtocolExtra().Flow.Should().BeEqualTo(source.GetProtocolExtra().Flow);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Tuic_ShouldRoundTripUserInfoAndCongestionControl()
    {
        var source = CreateTuicProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.TUIC);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().CongestionControl.Should()
            .BeEqualTo(source.GetProtocolExtra().CongestionControl);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Anytls_ShouldRoundTripBasicFields()
    {
        var source = CreateAnytlsProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.Anytls);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.StreamSecurity.Should().BeEqualTo(source.StreamSecurity);
        await resolved.Sni.Should().BeEqualTo(source.Sni);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Wireguard_ShouldRoundTripKeysAndInterface()
    {
        var source = CreateWireguardProfile();

        var resolved = await ExportThenImport(source);
        var extra = resolved.GetProtocolExtra();
        var sourceExtra = source.GetProtocolExtra();

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.WireGuard);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await extra.WgPublicKey.Should().BeEqualTo(sourceExtra.WgPublicKey);
        await extra.WgPresharedKey.Should().BeEqualTo(sourceExtra.WgPresharedKey);
        await extra.WgReserved.Should().BeEqualTo(sourceExtra.WgReserved);
        await extra.WgInterfaceAddress.Should().BeEqualTo(sourceExtra.WgInterfaceAddress);
        await extra.WgMtu.Should().BeEqualTo(sourceExtra.WgMtu);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Naive_ShouldRoundTripCredentialsOverHttps()
    {
        var source = CreateNaiveProfile(false);

        var resolved = await ExportThenImport(source, Global.NaiveHttpsProtocolShare);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.Naive);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().InsecureConcurrency.Should()
            .BeEqualTo(source.GetProtocolExtra().InsecureConcurrency);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_NaiveQuic_ShouldRoundTripQuicScheme()
    {
        var source = CreateNaiveProfile(true);

        var resolved = await ExportThenImport(source, Global.NaiveQuicProtocolShare);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.Naive);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.GetProtocolExtra().NaiveQuic.Should().BeTrue();
    }

    [Test]
    public async Task ResolveConfig_UnsupportedProtocol_ShouldReturnNull()
    {
        var resolved = FmtHandler.ResolveConfig("not-a-share-uri", out var msg);

        await resolved.Should().BeNull();
        await msg.Should().NotBeNull();
        await msg.Should().NotBeEmpty();
    }

    [Test]
    public async Task GetShareUri_UnsupportedConfigType_ShouldReturnNull()
    {
        var item = new ProfileItem { ConfigType = EConfigType.PolicyGroup, Remarks = "group", };

        var uri = FmtHandler.GetShareUri(item);

        await uri.Should().BeNull();
    }

    private static async Task<ProfileItem> ExportThenImport(ProfileItem source)
    {
        return await ExportThenImport(source, Global.ProtocolShares[source.ConfigType]);
    }

    private static async Task<ProfileItem> ExportThenImport(ProfileItem source, string expectedPrefix)
    {
        var uri = FmtHandler.GetShareUri(source);

        await uri.Should().NotBeNull();
        await uri.Should().NotBeEmpty();
        await uri!.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        var resolved = FmtHandler.ResolveConfig(uri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {uri}, msg: {msg}");
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

    private static ProfileItem CreateTrojanProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Trojan,
            Remarks = "trojan demo",
            Address = "trojan.example",
            Port = 443,
            Password = "trojan-pass",
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.StreamSecurity,
            Sni = "sni.trojan.example",
        };

        item.SetProtocolExtra(new ProtocolExtraItem { Flow = Global.Flows[1], });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateTuicProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.TUIC,
            Remarks = "tuic demo",
            Address = "tuic.example",
            Port = 8443,
            Username = Guid.NewGuid().ToString(),
            Password = "tuic-pass",
        };

        item.SetProtocolExtra(new ProtocolExtraItem { CongestionControl = "bbr", });

        return item;
    }

    private static ProfileItem CreateAnytlsProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Anytls,
            Remarks = "anytls demo",
            Address = "anytls.example",
            Port = 8443,
            Password = "anytls-pass",
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.StreamSecurity,
            Sni = "sni.anytls.example",
        };

        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateWireguardProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.WireGuard,
            Remarks = "wireguard demo",
            Address = "wg.example",
            Port = 51820,
            Password = "interface-private-key",
        };

        item.SetProtocolExtra(new ProtocolExtraItem
        {
            WgPublicKey = "peer-public-key",
            WgPresharedKey = "peer-preshared-key",
            WgReserved = "1,2,3",
            WgInterfaceAddress = "10.0.0.2/32",
            WgMtu = 1420,
        });

        return item;
    }

    private static ProfileItem CreateNaiveProfile(bool quic)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Naive,
            Remarks = quic ? "naive quic demo" : "naive https demo",
            Address = "naive.example",
            Port = 443,
            Username = "naive-user",
            Password = "naive-pass",
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.None,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { NaiveQuic = quic, InsecureConcurrency = 4, });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }
}
