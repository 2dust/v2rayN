namespace ServiceLib.ViewModels;

public class AddServerViewModel : MyReactiveObject
{
    [Reactive]
    public ProfileItem SelectedSource { get; set; }

    [Reactive]
    public string? CoreType { get; set; }

    [Reactive]
    public bool AllowInsecure { get; set; }

    [Reactive]
    public bool MuxEnabled { get; set; }

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

    [Reactive]
    public string WgPresharedKey { get; set; }

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
    public string HttpHeadersJson { get; set; }

    public IObservableCollection<HttpHeaderItem> HttpHeaderItems { get; } = new ObservableCollectionExtended<HttpHeaderItem>();

    [Reactive]
    public string Hy2RealmUrl { get; set; }

    [Reactive]
    public int GeckoMinPacketSize { get; set; }

    [Reactive]
    public int GeckoMaxPacketSize { get; set; }

    [Reactive]
    public string RawHeaderType { get; set; }

    [Reactive]
    public string Host { get; set; }

    [Reactive]
    public string Path { get; set; }

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

    [Reactive]
    public int? KcpMtu { get; set; }

    public string TransportHeaderType
    {
        get => SelectedSource.GetNetwork() switch
        {
            nameof(ETransport.raw) => RawHeaderType,
            nameof(ETransport.kcp) => KcpHeaderType,
            nameof(ETransport.xhttp) => XhttpMode,
            nameof(ETransport.grpc) => GrpcMode,
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
    public ReactiveCommand<Unit, Unit> AddHttpHeaderCmd { get; }
    public ReactiveCommand<HttpHeaderItem, Unit> RemoveHttpHeaderCmd { get; }
    public ReactiveCommand<Unit, Unit> CopyHttpHeadersJsonCmd { get; }

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
        AddHttpHeaderCmd = ReactiveCommand.Create(AddHttpHeader);
        RemoveHttpHeaderCmd = ReactiveCommand.Create<HttpHeaderItem>(RemoveHttpHeader);
        CopyHttpHeadersJsonCmd = ReactiveCommand.CreateFromTask(CopyHttpHeadersJsonAsync);

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
        AllowInsecure = SelectedSource?.GetAllowInsecure() == true;
        MuxEnabled = SelectedSource?.MuxEnabled == true;
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
        Uot = protocolExtra.Uot ?? false;
        CongestionControl = protocolExtra.CongestionControl ?? string.Empty;
        InsecureConcurrency = protocolExtra.InsecureConcurrency > 0 ? protocolExtra.InsecureConcurrency : null;
        NaiveQuic = protocolExtra.NaiveQuic ?? false;
        LoadHttpHeaders(protocolExtra.HttpHeaders);
        Hy2RealmUrl = protocolExtra.Hy2RealmUrl ?? string.Empty;
        GeckoMinPacketSize = protocolExtra.GeckoMinPacketSize.ToInt();
        GeckoMaxPacketSize = protocolExtra.GeckoMaxPacketSize.ToInt();

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
        Dictionary<string, string>? httpHeaders = null;
        if (SelectedSource.ConfigType == EConfigType.HTTP
            && !TryBuildHttpHeaders(out httpHeaders))
        {
            NoticeManager.Instance.Enqueue(ResUI.InvalidHttpOutboundHeaders);
            return;
        }
        SelectedSource.CoreType = CoreType.IsNullOrEmpty() ? null : Enum.Parse<ECoreType>(CoreType);
        SelectedSource.AllowInsecure = AllowInsecure ? Global.StringTrue : Global.StringFalse;
        SelectedSource.MuxEnabled = MuxEnabled;
        SelectedSource.Cert = Cert.IsNullOrEmpty() ? string.Empty : Cert;
        SelectedSource.CertSha = CertSha.IsNullOrEmpty() ? string.Empty : CertSha;
        if (!Global.Networks.Contains(SelectedSource.Network))
        {
            SelectedSource.Network = Global.DefaultNetwork;
        }

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
            HttpHeaders = SelectedSource.ConfigType == EConfigType.HTTP ? httpHeaders : null,
            WgPublicKey = WgPublicKey.NullIfEmpty(),
            WgPresharedKey = WgPresharedKey.NullIfEmpty(),
            WgInterfaceAddress = WgInterfaceAddress.NullIfEmpty(),
            WgReserved = WgReserved.NullIfEmpty(),
            WgMtu = WgMtu >= 576 ? WgMtu : null,
            Uot = Uot ? true : null,
            CongestionControl = CongestionControl.NullIfEmpty(),
            InsecureConcurrency = InsecureConcurrency > 0 ? InsecureConcurrency : null,
            NaiveQuic = NaiveQuic ? true : null,
            Hy2RealmUrl = realm?.ToUri().NullIfEmpty(),
            GeckoMinPacketSize = GeckoMinPacketSize > 0 ? GeckoMinPacketSize.ToString() : null,
            GeckoMaxPacketSize = GeckoMaxPacketSize > 0 ? GeckoMaxPacketSize.ToString() : null,
        });
        SelectedSource.SetTransportExtra(transport);

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

    private void LoadHttpHeaders(Dictionary<string, string>? headers)
    {
        HttpHeaderItems.Clear();
        if (headers?.Count > 0)
        {
            foreach (var item in headers)
            {
                AddHttpHeaderItem(item.Key, item.Value);
            }
        }

        if (HttpHeaderItems.Count == 0)
        {
            AddHttpHeaderItem();
        }
        RefreshHttpHeadersJson();
    }

    private void AddHttpHeader()
    {
        AddHttpHeaderItem();
        RefreshHttpHeadersJson();
    }

    private void AddHttpHeaderItem(string key = "", string value = "")
    {
        var item = new HttpHeaderItem
        {
            Key = key,
            Value = value
        };
        item.WhenAnyValue(x => x.Key, x => x.Value)
            .Subscribe(_ => RefreshHttpHeadersJson());
        HttpHeaderItems.Add(item);
    }

    private void RemoveHttpHeader(HttpHeaderItem item)
    {
        HttpHeaderItems.Remove(item);
        if (HttpHeaderItems.Count == 0)
        {
            AddHttpHeaderItem();
        }
        RefreshHttpHeadersJson();
    }

    private async Task CopyHttpHeadersJsonAsync()
    {
        RefreshHttpHeadersJson();
        if (_updateView != null)
        {
            await _updateView.Invoke(EViewAction.SetClipboardData, HttpHeadersJson);
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    private void RefreshHttpHeadersJson()
    {
        RefreshHttpHeaderWarnings();
        HttpHeadersJson = TryBuildHttpHeaders(out var headers, false) && headers?.Count > 0
            ? JsonUtils.Serialize(headers)
            : "{}";
    }

    private void RefreshHttpHeaderWarnings()
    {
        var keyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in HttpHeaderItems)
        {
            var key = item.Key?.Trim();
            if (key.IsNullOrEmpty())
            {
                continue;
            }

            keyCounts.TryGetValue(key, out var count);
            keyCounts[key] = count + 1;
        }

        foreach (var item in HttpHeaderItems)
        {
            var key = item.Key?.Trim();
            var hasDuplicateKey = !key.IsNullOrEmpty()
                                  && keyCounts.TryGetValue(key, out var count)
                                  && count > 1;
            item.HasWarning = hasDuplicateKey;
            item.WarningText = hasDuplicateKey ? ResUI.DuplicateHttpOutboundHeaderName : string.Empty;
        }
    }

    private bool TryBuildHttpHeaders(out Dictionary<string, string>? headers, bool validate = true)
    {
        headers = null;
        var parsedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in HttpHeaderItems)
        {
            var key = item.Key?.Trim();
            var value = item.Value ?? string.Empty;
            if (key.IsNullOrEmpty())
            {
                if (value.IsNullOrEmpty())
                {
                    continue;
                }
                return !validate;
            }

            if (parsedHeaders.ContainsKey(key))
            {
                return !validate;
            }

            parsedHeaders[key] = value;
        }

        headers = parsedHeaders.Count > 0 ? parsedHeaders : null;
        return true;
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
            _ => string.Empty,
        };
    }
}

public class HttpHeaderItem : MyReactiveObject
{
    [Reactive]
    public string Key { get; set; } = string.Empty;

    [Reactive]
    public string Value { get; set; } = string.Empty;

    [Reactive]
    public bool HasWarning { get; set; }

    [Reactive]
    public string WarningText { get; set; } = string.Empty;
}
