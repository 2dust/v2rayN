using ServiceLib.Models.Editors;

namespace ServiceLib.ViewModels;

public partial class AddServerViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial ProfileEditorModel SelectedSource { get; set; }

    [Reactive]
    public partial string? CoreType { get; set; }

    [Reactive]
    public partial bool AllowInsecure { get; set; }

    [Reactive]
    public partial bool MuxEnabled { get; set; }

    [Reactive]
    public partial string Cert { get; set; }

    [Reactive]
    public partial string CertTip { get; set; }

    [Reactive]
    public partial string CertSha { get; set; }

    [Reactive]
    public partial string SalamanderPass { get; set; }

    [Reactive]
    public partial int AlterId { get; set; }

    [Reactive]
    public partial string Ports { get; set; }

    [Reactive]
    public partial int? UpMbps { get; set; }

    [Reactive]
    public partial int? DownMbps { get; set; }

    [Reactive]
    public partial string HopInterval { get; set; }

    [Reactive]
    public partial string Flow { get; set; }

    [Reactive]
    public partial string VmessSecurity { get; set; }

    [Reactive]
    public partial string VlessEncryption { get; set; }

    [Reactive]
    public partial string SsMethod { get; set; }

    [Reactive]
    public partial string WgPublicKey { get; set; }

    [Reactive]
    public partial string WgPresharedKey { get; set; }

    [Reactive]
    public partial string WgInterfaceAddress { get; set; }

    [Reactive]
    public partial string WgReserved { get; set; }

    [Reactive]
    public partial int WgMtu { get; set; }

    [Reactive]
    public partial string WgDns { get; set; }

    [Reactive]
    public partial bool Uot { get; set; }

    [Reactive]
    public partial string CongestionControl { get; set; }

    [Reactive]
    public partial int? InsecureConcurrency { get; set; }

    [Reactive]
    public partial bool NaiveQuic { get; set; }

    [Reactive]
    public partial string HttpHeadersJson { get; set; }

    [Reactive]
    public partial string Hy2RealmUrl { get; set; }

    [Reactive]
    public partial int GeckoMinPacketSize { get; set; }

    [Reactive]
    public partial int GeckoMaxPacketSize { get; set; }

    [Reactive]
    public partial string MasquePath { get; set; }

    [Reactive]
    public partial string MasqueHeaders { get; set; }

    [Reactive]
    public partial string WarpPublicKey { get; set; }

    [Reactive]
    public partial string WarpPrivateKey { get; set; }

    [Reactive]
    public partial string WarpAddress { get; set; }

    [Reactive]
    public partial string Network { get; set; }

    [Reactive]
    public partial string RawHeaderType { get; set; }

    [Reactive]
    public partial string Host { get; set; }

    [Reactive]
    public partial string Path { get; set; }

    [Reactive]
    public partial string XhttpMode { get; set; }

    [Reactive]
    public partial string XhttpExtra { get; set; }

    [Reactive]
    public partial string GrpcAuthority { get; set; }

    [Reactive]
    public partial string GrpcServiceName { get; set; }

    [Reactive]
    public partial string GrpcMode { get; set; }

    [Reactive]
    public partial string KcpHeaderType { get; set; }

    [Reactive]
    public partial string KcpSeed { get; set; }

    [Reactive]
    public partial int? KcpMtu { get; set; }

    public string TransportHeaderType
    {
        get => GetCurrentNetwork() switch
        {
            nameof(ETransport.raw) => RawHeaderType,
            nameof(ETransport.kcp) => KcpHeaderType,
            nameof(ETransport.xhttp) => XhttpMode,
            nameof(ETransport.grpc) => GrpcMode,
            _ => string.Empty,
        };
        set
        {
            switch (GetCurrentNetwork())
            {
                case nameof(ETransport.raw):
                    RawHeaderType = value;
                    break;

                case nameof(ETransport.kcp):
                    KcpHeaderType = value;
                    break;

                case nameof(ETransport.xhttp):
                    XhttpMode = value;
                    break;

                case nameof(ETransport.grpc):
                    GrpcMode = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportHost
    {
        get => GetCurrentNetwork() switch
        {
            nameof(ETransport.raw) => Host,
            nameof(ETransport.ws) => Host,
            nameof(ETransport.httpupgrade) => Host,
            nameof(ETransport.xhttp) => Host,
            nameof(ETransport.grpc) => GrpcAuthority,
            _ => string.Empty,
        };
        set
        {
            switch (GetCurrentNetwork())
            {
                case nameof(ETransport.raw):
                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                case nameof(ETransport.xhttp):
                    Host = value;
                    break;

                case nameof(ETransport.grpc):
                    GrpcAuthority = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportPath
    {
        get => GetCurrentNetwork() switch
        {
            nameof(ETransport.kcp) => KcpSeed,
            nameof(ETransport.ws) => Path,
            nameof(ETransport.httpupgrade) => Path,
            nameof(ETransport.xhttp) => Path,
            nameof(ETransport.grpc) => GrpcServiceName,
            _ => string.Empty,
        };
        set
        {
            switch (GetCurrentNetwork())
            {
                case nameof(ETransport.kcp):
                    KcpSeed = value;
                    break;

                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                case nameof(ETransport.xhttp):
                    Path = value;
                    break;

                case nameof(ETransport.grpc):
                    GrpcServiceName = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportExtraText
    {
        get => GetCurrentNetwork() == nameof(ETransport.xhttp)
            ? XhttpExtra
            : string.Empty;
        set
        {
            if (GetCurrentNetwork() == nameof(ETransport.xhttp))
            {
                XhttpExtra = value;
            }
            this.RaisePropertyChanged();
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> FetchCertCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FetchCertChainCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public AddServerViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;

        FetchCertCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FetchCert();
        });
        FetchCertChainCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FetchCertChain();
        });
        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });
        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertTip());

        this.WhenAnyValue(x => x.CertSha)
            .Subscribe(_ => UpdateCertTip());

        this.WhenAnyValue(x => x.Network)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TransportHeaderType));
                this.RaisePropertyChanged(nameof(TransportHost));
                this.RaisePropertyChanged(nameof(TransportPath));
                this.RaisePropertyChanged(nameof(TransportExtraText));
            });

        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertSha());

        SelectedSource = ProfileEditorModel.FromEntity(profileItem);
        if (profileItem.IndexId.IsNullOrEmpty())
        {
            SelectedSource.StreamSecurity = "";
        }
        CoreType = profileItem.CoreType?.ToString();
        AllowInsecure = profileItem.GetAllowInsecure() == true;
        MuxEnabled = profileItem.MuxEnabled == true;
        Network = profileItem.IndexId.IsNullOrEmpty() ? Global.DefaultNetwork : profileItem.Network ?? Global.DefaultNetwork;
        Cert = profileItem.Cert ?? string.Empty;
        CertSha = profileItem.CertSha ?? string.Empty;

        var protocolExtra = profileItem.GetProtocolExtra() ?? new();
        var transport = profileItem.GetTransportExtra() ?? new();
        Ports = protocolExtra.Ports ?? string.Empty;
        AlterId = int.TryParse(protocolExtra.AlterId, out var result) ? result : 0;
        Flow = protocolExtra.Flow ?? string.Empty;
        SalamanderPass = protocolExtra.SalamanderPass ?? string.Empty;
        UpMbps = protocolExtra.UpMbps;
        DownMbps = protocolExtra.DownMbps;
        HopInterval = protocolExtra.HopInterval ?? string.Empty;
        VmessSecurity = protocolExtra.VmessSecurity?.IsNullOrEmpty() == false ? protocolExtra.VmessSecurity : Global.DefaultSecurity;
        VlessEncryption = protocolExtra.VlessEncryption?.IsNullOrEmpty() == false ? protocolExtra.VlessEncryption : Global.None;
        SsMethod = protocolExtra.SsMethod ?? string.Empty;
        WgPublicKey = protocolExtra.WgPublicKey ?? string.Empty;
        WgPresharedKey = protocolExtra.WgPresharedKey ?? string.Empty;
        WgInterfaceAddress = protocolExtra.WgInterfaceAddress ?? string.Empty;
        WgReserved = protocolExtra.WgReserved ?? string.Empty;
        WgMtu = protocolExtra.WgMtu ?? 1280;
        WgDns = protocolExtra.WgDns ?? string.Empty;
        Uot = protocolExtra.Uot ?? false;
        CongestionControl = protocolExtra.CongestionControl ?? string.Empty;
        InsecureConcurrency = protocolExtra.InsecureConcurrency > 0 ? protocolExtra.InsecureConcurrency : null;
        NaiveQuic = protocolExtra.NaiveQuic ?? false;
        HttpHeadersJson = protocolExtra.HttpHeaders ?? string.Empty;
        Hy2RealmUrl = protocolExtra.Hy2RealmUrl ?? string.Empty;
        GeckoMinPacketSize = protocolExtra.GeckoMinPacketSize.ToInt();
        GeckoMaxPacketSize = protocolExtra.GeckoMaxPacketSize.ToInt();
        MasquePath = protocolExtra.MasquePath ?? string.Empty;
        MasqueHeaders = protocolExtra.MasqueHeaders ?? string.Empty;
        WarpPublicKey = protocolExtra.WarpPublicKey ?? string.Empty;
        WarpPrivateKey = protocolExtra.WarpPrivateKey ?? string.Empty;
        WarpAddress = protocolExtra.WarpAddress ?? string.Empty;

        RawHeaderType = transport.RawHeaderType ?? Global.None;
        Host = transport.Host ?? string.Empty;
        Path = transport.Path ?? string.Empty;
        XhttpMode = transport.XhttpMode ?? Global.DefaultXhttpMode;
        XhttpExtra = transport.XhttpExtra ?? string.Empty;
        GrpcAuthority = transport.GrpcAuthority ?? string.Empty;
        GrpcServiceName = transport.GrpcServiceName ?? string.Empty;
        GrpcMode = transport.GrpcMode.IsNullOrEmpty() ? Global.GrpcGunMode : transport.GrpcMode;
        KcpHeaderType = transport.KcpHeaderType.IsNullOrEmpty() ? Global.None : transport.KcpHeaderType;
        KcpSeed = transport.KcpSeed ?? string.Empty;
        KcpMtu = transport.KcpMtu;
    }

    private async Task SaveServerAsync()
    {
        if (SelectedSource.Remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        if (SelectedSource.Address.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.FillServerAddress);
            return;
        }
        var port = SelectedSource.Port.ToString();
        if (port.IsNullOrEmpty() || !Utils.IsNumeric(port)
            || SelectedSource.Port <= 0 || SelectedSource.Port >= Global.MaxPort)
        {
            NoticeManager.Instance.Enqueue(ResUI.FillCorrectServerPort);
            return;
        }
        if (SelectedSource.ConfigType == EConfigType.Shadowsocks)
        {
            if (SelectedSource.Password.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.FillPassword);
                return;
            }
            if (SsMethod.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.PleaseSelectEncryption);
                return;
            }
        }
        if (SelectedSource.ConfigType is not EConfigType.SOCKS and not EConfigType.HTTP)
        {
            if (SelectedSource.Password.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.FillUUID);
                return;
            }
        }
        HyRealm? realm = null;
        if (!Hy2RealmUrl.IsNullOrEmpty())
        {
            var realmResult = HyRealm.TryParse(Hy2RealmUrl, out realm);
            if (!realmResult)
            {
                NoticeManager.Instance.Enqueue(ResUI.InvalidHy2RealmUrl);
                return;
            }
        }
        if (HttpHeadersJson.IsNotEmpty() && JsonUtils.ParseJson(HttpHeadersJson) == null)
        {
            NoticeManager.Instance.Enqueue(ResUI.InvalidHttpOutboundHeaders);
            return;
        }
        var item = SelectedSource.ToEntity();
        item.CoreType = CoreType.IsNullOrEmpty() ? null : Enum.Parse<ECoreType>(CoreType);
        item.AllowInsecure = AllowInsecure ? Global.StringTrue : Global.StringFalse;
        item.MuxEnabled = MuxEnabled;
        item.Cert = Cert.IsNullOrEmpty() ? string.Empty : Cert;
        item.CertSha = CertSha.IsNullOrEmpty() ? string.Empty : CertSha;
        if (!Global.Networks.Contains(Network))
        {
            Network = Global.DefaultNetwork;
        }
        item.Network = Network;

        var transport = new TransportExtraItem
        {
            RawHeaderType = RawHeaderType.NullIfEmpty(),
            Host = Host.NullIfEmpty(),
            Path = Path.NullIfEmpty(),
            XhttpMode = XhttpMode.NullIfEmpty(),
            XhttpExtra = XhttpExtra.NullIfEmpty(),
            GrpcAuthority = GrpcAuthority.NullIfEmpty(),
            GrpcServiceName = GrpcServiceName.NullIfEmpty(),
            GrpcMode = GrpcMode.NullIfEmpty(),
            KcpHeaderType = KcpHeaderType.NullIfEmpty(),
            KcpSeed = KcpSeed.NullIfEmpty(),
            KcpMtu = KcpMtu > 0 ? KcpMtu : null,
        };

        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            Ports = Ports.NullIfEmpty(),
            AlterId = AlterId > 0 ? AlterId.ToString() : null,
            Flow = Flow.NullIfEmpty(),
            SalamanderPass = SalamanderPass.NullIfEmpty(),
            UpMbps = UpMbps,
            DownMbps = DownMbps,
            HopInterval = HopInterval.NullIfEmpty(),
            VmessSecurity = VmessSecurity.NullIfEmpty(),
            VlessEncryption = VlessEncryption.NullIfEmpty(),
            SsMethod = SsMethod.NullIfEmpty(),
            HttpHeaders = item.ConfigType == EConfigType.HTTP ? HttpHeadersJson.NullIfEmpty() : null,
            WgPublicKey = WgPublicKey.NullIfEmpty(),
            WgPresharedKey = WgPresharedKey.NullIfEmpty(),
            WgInterfaceAddress = WgInterfaceAddress.NullIfEmpty(),
            WgReserved = WgReserved.NullIfEmpty(),
            WgMtu = WgMtu >= 576 ? WgMtu : null,
            WgDns = WgDns.NullIfEmpty(),
            Uot = Uot ? true : null,
            CongestionControl = CongestionControl.NullIfEmpty(),
            InsecureConcurrency = InsecureConcurrency > 0 ? InsecureConcurrency : null,
            NaiveQuic = NaiveQuic ? true : null,
            Hy2RealmUrl = realm?.ToUri().NullIfEmpty(),
            GeckoMinPacketSize = GeckoMinPacketSize > 0 ? GeckoMinPacketSize.ToString() : null,
            GeckoMaxPacketSize = GeckoMaxPacketSize > 0 ? GeckoMaxPacketSize.ToString() : null,
            MasquePath = MasquePath.NullIfEmpty(),
            MasqueHeaders = MasqueHeaders.NullIfEmpty(),
            WarpPublicKey = WarpPublicKey.NullIfEmpty(),
            WarpPrivateKey = WarpPrivateKey.NullIfEmpty(),
            WarpAddress = WarpAddress.NullIfEmpty(),
        });
        item.SetTransportExtra(transport);

        if (await ConfigHandler.AddServer(_config, item) == 0)
        {
            SelectedSource = ProfileEditorModel.FromEntity(item);
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    private void UpdateCertTip(string? errorMessage = null)
    {
        CertTip = errorMessage.IsNullOrEmpty()
            ? ((Cert.IsNullOrEmpty() && CertSha.IsNullOrEmpty()) ? ResUI.CertNotSet : ResUI.CertSet)
            : errorMessage;
    }

    private void UpdateCertSha()
    {
        if (Cert.IsNullOrEmpty())
        {
            return;
        }

        var certList = CertPemManager.ParsePemChain(Cert);
        if (certList.Count == 0)
        {
            return;
        }

        List<string> shaList = [];
        foreach (var cert in certList)
        {
            var sha = CertPemManager.GetCertSha256Thumbprint(cert);
            if (sha.IsNullOrEmpty())
            {
                return;
            }
            shaList.Add(sha);
        }
        CertSha = string.Join(',', shaList);
    }

    private async Task FetchCert()
    {
        var item = SelectedSource.ToEntity();
        if (item.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = item.Address;
        var serverName = item.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = GetCurrentTransportHost();
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = item.Address;
        }
        if (item.Port > 0)
        {
            domain += $":{item.Port}";
        }

        (Cert, var certError) = await CertPemManager.Instance.GetCertPemAsync(domain, serverName,
            verifyPeerCertByName: Utils.String2List(item.VerifyPeerCertByName));
        UpdateCertTip(certError);
    }

    private async Task FetchCertChain()
    {
        var item = SelectedSource.ToEntity();
        if (item.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = item.Address;
        var serverName = item.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = GetCurrentTransportHost();
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = item.Address;
        }
        if (item.Port > 0)
        {
            domain += $":{item.Port}";
        }

        var (certs, certError) = await CertPemManager.Instance.GetCertChainPemAsync(domain, serverName,
            verifyPeerCertByName: Utils.String2List(item.VerifyPeerCertByName));
        Cert = CertPemManager.ConcatenatePemChain(certs);
        UpdateCertTip(certError);
    }

    private string GetCurrentNetwork()
    {
        return new ProfileItem { Network = Network }.GetNetwork();
    }

    private string GetCurrentTransportHost()
    {
        return GetCurrentNetwork() switch
        {
            nameof(ETransport.raw) => Host,
            nameof(ETransport.ws) => Host,
            nameof(ETransport.httpupgrade) => Host,
            nameof(ETransport.xhttp) => Host,
            nameof(ETransport.grpc) => GrpcAuthority,
            _ => string.Empty,
        };
    }
}
