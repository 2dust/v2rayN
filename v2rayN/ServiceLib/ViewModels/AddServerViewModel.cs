

namespace ServiceLib.ViewModels;

public partial class AddServerViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial ProfileItem SelectedSource { get; set; }

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

    [Reactive]
    public partial string XdriveProvider { get; set; }

    [Reactive]
    public partial string XdriveService { get; set; }

    [Reactive]
    public partial string XdriveRemoteFolder { get; set; }

    [Reactive]
    public partial string XdriveSecrets { get; set; }

    [Reactive]
    public partial string XdriveS3Endpoint { get; set; }

    [Reactive]
    public partial string XdriveS3Region { get; set; }

    [Reactive]
    public partial string XdriveS3Bucket { get; set; }

    [Reactive]
    public partial bool? XdriveS3PathStyle { get; set; }

    [Reactive]
    public partial string XdriveExtra { get; set; }

    public string TransportHeaderType
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => RawHeaderType,
            nameof(ETransport.kcp) => KcpHeaderType,
            nameof(ETransport.xhttp) => XhttpMode,
            nameof(ETransport.grpc) => GrpcMode,
            nameof(ETransport.xdrive) => XdriveService,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
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

                case nameof(ETransport.xdrive):
                    XdriveService = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportHost
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => Host,
            nameof(ETransport.ws) => Host,
            nameof(ETransport.httpupgrade) => Host,
            nameof(ETransport.xhttp) => Host,
            nameof(ETransport.grpc) => GrpcAuthority,
            nameof(ETransport.xdrive) => XdriveS3Endpoint,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
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

                case nameof(ETransport.xdrive):
                    XdriveS3Endpoint = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportPath
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.kcp) => KcpSeed,
            nameof(ETransport.ws) => Path,
            nameof(ETransport.httpupgrade) => Path,
            nameof(ETransport.xhttp) => Path,
            nameof(ETransport.grpc) => GrpcServiceName,
            nameof(ETransport.xdrive) => XdriveRemoteFolder,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
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

                case nameof(ETransport.xdrive):
                    XdriveRemoteFolder = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public string TransportExtraText
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.xhttp) => XhttpExtra,
            nameof(ETransport.xdrive) => XdriveExtra,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
            {
                case nameof(ETransport.xhttp):
                    XhttpExtra = value;
                    break;

                case nameof(ETransport.xdrive):
                    XdriveExtra = value;
                    break;
            }
            this.RaisePropertyChanged();
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> FetchCertCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FetchCertChainCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public AddServerViewModel(ProfileItem profileItem)
    {
        _config = ConfigTextManager.Instance.Config;

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
            .Subscribe(network =>
            {
                if (SelectedSource != null && !network.IsNullOrEmpty())
                {
                    SelectedSource.Network = network;
                }
                if (network == nameof(ETransport.xdrive))
                {
                    MuxEnabled = true;
                }
                this.RaisePropertyChanged(nameof(TransportHeaderType));
                this.RaisePropertyChanged(nameof(TransportHost));
                this.RaisePropertyChanged(nameof(TransportPath));
                this.RaisePropertyChanged(nameof(TransportExtraText));
            });

        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertSha());

        this.WhenAnyValue(x => x.XdriveProvider)
            .Where(p => p.IsNotEmpty())
            .Subscribe(provider =>
            {
                ApplyXdriveProviderPreset(provider);
            });

        if (profileItem.IndexId.IsNullOrEmpty())
        {
            profileItem.Network = Global.DefaultNetwork;
            profileItem.StreamSecurity = "";
            SelectedSource = profileItem;
        }
        else
        {
            SelectedSource = JsonUtils.DeepCopy(profileItem);
        }
        CoreType = SelectedSource?.CoreType?.ToString();
        AllowInsecure = SelectedSource?.GetAllowInsecure() == true;
        MuxEnabled = SelectedSource?.MuxEnabled == true;
        Network = SelectedSource?.Network ?? Global.DefaultNetwork;
        Cert = SelectedSource?.Cert ?? string.Empty;
        CertSha = SelectedSource?.CertSha ?? string.Empty;

        var protocolExtra = SelectedSource?.GetProtocolExtra() ?? new();
        var transport = SelectedSource?.GetTransportExtra() ?? new();
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
        XdriveService = transport.XdriveService.IsNullOrEmpty() ? (Global.XdriveServices.FirstOrDefault() ?? "google-drive") : transport.XdriveService;
        XdriveRemoteFolder = transport.XdriveRemoteFolder ?? string.Empty;
        XdriveSecrets = transport.XdriveSecrets ?? string.Empty;
        XdriveS3Endpoint = transport.XdriveS3Endpoint ?? string.Empty;
        XdriveS3Region = transport.XdriveS3Region ?? string.Empty;
        XdriveS3Bucket = transport.XdriveS3Bucket ?? string.Empty;
        XdriveS3PathStyle = transport.XdriveS3PathStyle;
        XdriveExtra = transport.XdriveExtra ?? string.Empty;

        if (transport.XdriveService == "google-drive")
        {
            XdriveProvider = "Google Drive";
        }
        else if (transport.XdriveS3Endpoint?.Contains("r2.cloudflarestorage.com") == true)
        {
            XdriveProvider = "Cloudflare R2";
        }
        else if (transport.XdriveS3Endpoint?.Contains("amazonaws.com") == true)
        {
            XdriveProvider = "AWS S3 (Global)";
        }
        else if (transport.XdriveS3Endpoint?.Contains("yandexcloud.net") == true)
        {
            XdriveProvider = "Yandex Cloud S3";
        }
        else if (transport.XdriveS3Endpoint?.Contains("arvanstorage.ir") == true)
        {
            XdriveProvider = "ArvanCloud S3";
        }
        else if (SelectedSource.Remarks?.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) == true
                 || (transport.XdriveS3PathStyle == true && transport.XdriveS3Region == "auto"))
        {
            XdriveProvider = "Cloudflare R2";
        }
        else if (transport.XdriveService == "s3" || transport.XdriveS3Endpoint.IsNotEmpty())
        {
            XdriveProvider = "Custom S3";
        }
        else
        {
            XdriveProvider = "Cloudflare R2";
        }
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
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : Enum.Parse<ECoreType>(CoreType);
        SelectedSource.AllowInsecure = AllowInsecure ? Global.StringTrue : Global.StringFalse;
        SelectedSource.MuxEnabled = MuxEnabled;
        SelectedSource.Cert = Cert.IsNullOrEmpty() ? string.Empty : Cert;
        SelectedSource.CertSha = CertSha.IsNullOrEmpty() ? string.Empty : CertSha;
        if (!Global.Networks.Contains(Network))
        {
            Network = Global.DefaultNetwork;
        }
        SelectedSource.Network = Network;

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
            XdriveService = XdriveService.NullIfEmpty(),
            XdriveRemoteFolder = XdriveRemoteFolder.NullIfEmpty(),
            XdriveSecrets = XdriveSecrets.NullIfEmpty(),
            XdriveS3Endpoint = XdriveS3Endpoint.NullIfEmpty(),
            XdriveS3Region = XdriveS3Region.NullIfEmpty(),
            XdriveS3Bucket = XdriveS3Bucket.NullIfEmpty(),
            XdriveS3PathStyle = XdriveS3PathStyle,
            XdriveExtra = XdriveExtra.NullIfEmpty(),
        };

        SelectedSource.SetProtocolExtra(SelectedSource.GetProtocolExtra() with
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
            HttpHeaders = SelectedSource.ConfigType == EConfigType.HTTP ? HttpHeadersJson.NullIfEmpty() : null,
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
        SelectedSource.SetTransportExtra(transport);

        if (await ConfigDbManager.AddServer(_config, SelectedSource) == 0)
        {
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

    private void ApplyXdriveProviderPreset(string provider)
    {
        switch (provider)
        {
            case "Cloudflare R2":
                XdriveService = "s3";
                if (XdriveS3Region.IsNullOrEmpty())
                {
                    XdriveS3Region = "auto";
                }

                XdriveS3PathStyle = true;
                break;
            case "Google Drive":
                XdriveService = "google-drive";
                SelectedSource.StreamSecurity = Global.StreamSecurity;
                if (SelectedSource.Sni.IsNullOrEmpty())
                {
                    SelectedSource.Sni = "www.google.com";
                }

                if (SelectedSource.Alpn.IsNullOrEmpty())
                {
                    SelectedSource.Alpn = "h2";
                }

                if (SelectedSource.Address.IsNullOrEmpty())
                {
                    SelectedSource.Address = "www.google.com";
                }

                if (SelectedSource.Port <= 0)
                {
                    SelectedSource.Port = 443;
                }

                break;
            case "AWS S3 (Global)":
                XdriveService = "s3";
                if (XdriveS3Region.IsNullOrEmpty())
                {
                    XdriveS3Region = "ap-northeast-1";
                }

                XdriveS3PathStyle = false;
                break;
            case "Yandex Cloud S3":
                XdriveService = "s3";
                if (XdriveS3Region.IsNullOrEmpty())
                {
                    XdriveS3Region = "ru-central1";
                }

                XdriveS3PathStyle = true;
                break;
            case "ArvanCloud S3":
                XdriveService = "s3";
                if (XdriveS3Region.IsNullOrEmpty())
                {
                    XdriveS3Region = "ir-tbz-sh1";
                }

                XdriveS3PathStyle = true;
                break;
            case "Custom S3":
                XdriveService = "s3";
                break;
        }
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
        if (SelectedSource.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = SelectedSource.Address;
        var serverName = SelectedSource.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = GetCurrentTransportHost();
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }

        (Cert, var certError) = await CertPemManager.Instance.GetCertPemAsync(domain, serverName,
            verifyPeerCertByName: Utils.String2List(SelectedSource.VerifyPeerCertByName));
        UpdateCertTip(certError);
    }

    private async Task FetchCertChain()
    {
        if (SelectedSource.StreamSecurity != Global.StreamSecurity)
        {
            return;
        }
        var domain = SelectedSource.Address;
        var serverName = SelectedSource.Sni;
        if (serverName.IsNullOrEmpty())
        {
            serverName = GetCurrentTransportHost();
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }

        var (certs, certError) = await CertPemManager.Instance.GetCertChainPemAsync(domain, serverName,
            verifyPeerCertByName: Utils.String2List(SelectedSource.VerifyPeerCertByName));
        Cert = CertPemManager.ConcatenatePemChain(certs);
        UpdateCertTip(certError);
    }

    private string GetCurrentTransportHost()
    {
        return SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => Host,
            nameof(ETransport.ws) => Host,
            nameof(ETransport.httpupgrade) => Host,
            nameof(ETransport.xhttp) => Host,
            nameof(ETransport.grpc) => GrpcAuthority,
            nameof(ETransport.xdrive) => XdriveS3Endpoint,
            _ => string.Empty,
        };
    }
}
