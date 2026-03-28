namespace ServiceLib.ViewModels;

public class AddServerViewModel : MyReactiveObject
{
    [Reactive]
    public ProfileItem SelectedSource { get; set; }

    [Reactive]
    public string? CoreType { get; set; }

    [Reactive]
    public string Cert { get; set; }

    [Reactive]
    public string CertTip { get; set; }

    [Reactive]
    public string CertSha { get; set; }

    [Reactive]
    public string SalamanderPass { get; set; }

    [Reactive]
    public int AlterId { get; set; }

    [Reactive]
    public string Ports { get; set; }

    [Reactive]
    public int? UpMbps { get; set; }

    [Reactive]
    public int? DownMbps { get; set; }

    [Reactive]
    public string HopInterval { get; set; }

    [Reactive]
    public string Flow { get; set; }

    [Reactive]
    public string VmessSecurity { get; set; }

    [Reactive]
    public string VlessEncryption { get; set; }

    [Reactive]
    public string SsMethod { get; set; }

    [Reactive]
    public string WgPublicKey { get; set; }

    //[Reactive]
    //public string WgPresharedKey { get; set; }
    [Reactive]
    public string WgInterfaceAddress { get; set; }

    [Reactive]
    public string WgReserved { get; set; }

    [Reactive]
    public int WgMtu { get; set; }

    [Reactive]
    public bool Uot { get; set; }

    [Reactive]
    public string CongestionControl { get; set; }

    [Reactive]
    public int? InsecureConcurrency { get; set; }

    [Reactive]
    public bool NaiveQuic { get; set; }

    [Reactive]
    public string TcpHeaderType { get; set; }

    [Reactive]
    public string TcpHost { get; set; }

    [Reactive]
    public string WsHost { get; set; }

    [Reactive]
    public string WsPath { get; set; }

    [Reactive]
    public string HttpupgradeHost { get; set; }

    [Reactive]
    public string HttpupgradePath { get; set; }

    [Reactive]
    public string XhttpHost { get; set; }

    [Reactive]
    public string XhttpPath { get; set; }

    [Reactive]
    public string XhttpMode { get; set; }

    [Reactive]
    public string XhttpExtra { get; set; }

    [Reactive]
    public string GrpcAuthority { get; set; }

    [Reactive]
    public string GrpcServiceName { get; set; }

    [Reactive]
    public string GrpcMode { get; set; }

    [Reactive]
    public string KcpHeaderType { get; set; }

    [Reactive]
    public string KcpSeed { get; set; }

    public string TransportHeaderType
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.tcp) => TcpHeaderType,
            nameof(ETransport.kcp) => KcpHeaderType,
            nameof(ETransport.xhttp) => XhttpMode,
            nameof(ETransport.grpc) => GrpcMode,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
            {
                case nameof(ETransport.tcp):
                    TcpHeaderType = value;
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
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.tcp) => TcpHost,
            nameof(ETransport.ws) => WsHost,
            nameof(ETransport.httpupgrade) => HttpupgradeHost,
            nameof(ETransport.xhttp) => XhttpHost,
            nameof(ETransport.grpc) => GrpcAuthority,
            _ => string.Empty,
        };
        set
        {
            switch (SelectedSource.GetNetwork())
            {
                case nameof(ETransport.tcp):
                    TcpHost = value;
                    break;
                case nameof(ETransport.ws):
                    WsHost = value;
                    break;
                case nameof(ETransport.httpupgrade):
                    HttpupgradeHost = value;
                    break;
                case nameof(ETransport.xhttp):
                    XhttpHost = value;
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
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.kcp) => KcpSeed,
            nameof(ETransport.ws) => WsPath,
            nameof(ETransport.httpupgrade) => HttpupgradePath,
            nameof(ETransport.xhttp) => XhttpPath,
            nameof(ETransport.grpc) => GrpcServiceName,
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
                    WsPath = value;
                    break;
                case nameof(ETransport.httpupgrade):
                    HttpupgradePath = value;
                    break;
                case nameof(ETransport.xhttp):
                    XhttpPath = value;
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
        get => SelectedSource.GetNetwork() == nameof(ETransport.xhttp)
            ? XhttpExtra
            : string.Empty;
        set
        {
            if (SelectedSource.GetNetwork() == nameof(ETransport.xhttp))
            {
                XhttpExtra = value;
            }
            this.RaisePropertyChanged();
        }
    }

    public ReactiveCommand<Unit, Unit> FetchCertCmd { get; }
    public ReactiveCommand<Unit, Unit> FetchCertChainCmd { get; }
    public ReactiveCommand<Unit, Unit> SaveCmd { get; }

    public AddServerViewModel(ProfileItem profileItem, Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

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

        this.WhenAnyValue(x => x.SelectedSource.Network)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TransportHeaderType));
                this.RaisePropertyChanged(nameof(TransportHost));
                this.RaisePropertyChanged(nameof(TransportPath));
                this.RaisePropertyChanged(nameof(TransportExtraText));
            });

        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertSha());

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
        Cert = SelectedSource?.Cert?.ToString() ?? string.Empty;
        CertSha = SelectedSource?.CertSha?.ToString() ?? string.Empty;

        var protocolExtra = SelectedSource?.GetProtocolExtra();
        var transport = protocolExtra?.Transport ?? new TransportExtra();
        Ports = protocolExtra?.Ports ?? string.Empty;
        AlterId = int.TryParse(protocolExtra?.AlterId, out var result) ? result : 0;
        Flow = protocolExtra?.Flow ?? string.Empty;
        SalamanderPass = protocolExtra?.SalamanderPass ?? string.Empty;
        UpMbps = protocolExtra?.UpMbps;
        DownMbps = protocolExtra?.DownMbps;
        HopInterval = protocolExtra?.HopInterval.IsNullOrEmpty() ?? true ? Global.Hysteria2DefaultHopInt.ToString() : protocolExtra.HopInterval;
        VmessSecurity = protocolExtra?.VmessSecurity?.IsNullOrEmpty() == false ? protocolExtra.VmessSecurity : Global.DefaultSecurity;
        VlessEncryption = protocolExtra?.VlessEncryption.IsNullOrEmpty() == false ? protocolExtra.VlessEncryption : Global.None;
        SsMethod = protocolExtra?.SsMethod ?? string.Empty;
        WgPublicKey = protocolExtra?.WgPublicKey ?? string.Empty;
        WgInterfaceAddress = protocolExtra?.WgInterfaceAddress ?? string.Empty;
        WgReserved = protocolExtra?.WgReserved ?? string.Empty;
        WgMtu = protocolExtra?.WgMtu ?? 1280;
        Uot = protocolExtra?.Uot ?? false;
        CongestionControl = protocolExtra?.CongestionControl ?? string.Empty;
        InsecureConcurrency = protocolExtra?.InsecureConcurrency > 0 ? protocolExtra.InsecureConcurrency : null;
        NaiveQuic = protocolExtra?.NaiveQuic ?? false;

        TcpHeaderType = transport.TcpHeaderType ?? Global.None;
        TcpHost = transport.TcpHost ?? string.Empty;
        WsHost = transport.WsHost ?? string.Empty;
        WsPath = transport.WsPath ?? string.Empty;
        HttpupgradeHost = transport.HttpupgradeHost ?? string.Empty;
        HttpupgradePath = transport.HttpupgradePath ?? string.Empty;
        XhttpHost = transport.XhttpHost ?? string.Empty;
        XhttpPath = transport.XhttpPath ?? string.Empty;
        XhttpMode = transport.XhttpMode ?? string.Empty;
        XhttpExtra = transport.XhttpExtra ?? string.Empty;
        GrpcAuthority = transport.GrpcAuthority ?? string.Empty;
        GrpcServiceName = transport.GrpcServiceName ?? string.Empty;
        GrpcMode = transport.GrpcMode.IsNullOrEmpty() ? Global.GrpcGunMode : transport.GrpcMode;
        KcpHeaderType = transport.KcpHeaderType.IsNullOrEmpty() ? Global.None : transport.KcpHeaderType;
        KcpSeed = transport.KcpSeed ?? string.Empty;

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
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : (ECoreType)Enum.Parse(typeof(ECoreType), CoreType);
        SelectedSource.Cert = Cert.IsNullOrEmpty() ? string.Empty : Cert;
        SelectedSource.CertSha = CertSha.IsNullOrEmpty() ? string.Empty : CertSha;
        if (!Global.Networks.Contains(SelectedSource.Network))
        {
            SelectedSource.Network = Global.DefaultNetwork;
        }

        var transport = new TransportExtra
        {
            TcpHeaderType = TcpHeaderType.NullIfEmpty(),
            TcpHost = TcpHost.NullIfEmpty(),
            WsHost = WsHost.NullIfEmpty(),
            WsPath = WsPath.NullIfEmpty(),
            HttpupgradeHost = HttpupgradeHost.NullIfEmpty(),
            HttpupgradePath = HttpupgradePath.NullIfEmpty(),
            XhttpHost = XhttpHost.NullIfEmpty(),
            XhttpPath = XhttpPath.NullIfEmpty(),
            XhttpMode = XhttpMode.NullIfEmpty(),
            XhttpExtra = XhttpExtra.NullIfEmpty(),
            GrpcAuthority = GrpcAuthority.NullIfEmpty(),
            GrpcServiceName = GrpcServiceName.NullIfEmpty(),
            GrpcMode = GrpcMode.NullIfEmpty(),
            KcpHeaderType = KcpHeaderType.NullIfEmpty(),
            KcpSeed = KcpSeed.NullIfEmpty(),
        };

        SelectedSource.SetProtocolExtra(SelectedSource.GetProtocolExtra() with
        {
            Transport = transport,
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
            WgPublicKey = WgPublicKey.NullIfEmpty(),
            WgInterfaceAddress = WgInterfaceAddress.NullIfEmpty(),
            WgReserved = WgReserved.NullIfEmpty(),
            WgMtu = WgMtu >= 576 ? WgMtu : null,
            Uot = Uot ? true : null,
            CongestionControl = CongestionControl.NullIfEmpty(),
            InsecureConcurrency = InsecureConcurrency > 0 ? InsecureConcurrency : null,
            NaiveQuic = NaiveQuic ? true : null,
        });

        if (await ConfigHandler.AddServer(_config, SelectedSource) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
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

        (Cert, var certError) = await CertPemManager.Instance.GetCertPemAsync(domain, serverName);
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

        var (certs, certError) = await CertPemManager.Instance.GetCertChainPemAsync(domain, serverName);
        Cert = CertPemManager.ConcatenatePemChain(certs);
        UpdateCertTip(certError);
    }

    private string GetCurrentTransportHost()
    {
        return SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.tcp) => TcpHost,
            nameof(ETransport.ws) => WsHost,
            nameof(ETransport.httpupgrade) => HttpupgradeHost,
            nameof(ETransport.xhttp) => XhttpHost,
            nameof(ETransport.grpc) => GrpcAuthority,
            _ => string.Empty,
        };
    }
}
