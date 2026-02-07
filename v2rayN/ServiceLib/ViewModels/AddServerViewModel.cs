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
    public int UpMbps { get; set; }

    [Reactive]
    public int DownMbps { get; set; }

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

        this.WhenAnyValue(x => x.Cert)
            .Subscribe(_ => UpdateCertSha());

        if (profileItem.IndexId.IsNullOrEmpty())
        {
            profileItem.Network = Global.DefaultNetwork;
            profileItem.HeaderType = Global.None;
            profileItem.RequestHost = "";
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
        Ports = protocolExtra?.Ports ?? string.Empty;
        AlterId = int.TryParse(protocolExtra?.AlterId, out var result) ? result : 0;
        Flow = protocolExtra?.Flow ?? string.Empty;
        SalamanderPass = protocolExtra?.SalamanderPass ?? string.Empty;
        UpMbps = protocolExtra?.UpMbps ?? _config.HysteriaItem.UpMbps;
        DownMbps = protocolExtra?.DownMbps ?? _config.HysteriaItem.DownMbps;
        HopInterval = protocolExtra?.HopInterval.IsNullOrEmpty() ?? true ? Global.Hysteria2DefaultHopInt.ToString() : protocolExtra.HopInterval;
        VmessSecurity = protocolExtra?.VmessSecurity?.IsNullOrEmpty() == false ? protocolExtra.VmessSecurity : Global.DefaultSecurity;
        VlessEncryption = protocolExtra?.VlessEncryption.IsNullOrEmpty() == false ? protocolExtra.VlessEncryption : Global.None;
        SsMethod = protocolExtra?.SsMethod ?? string.Empty;
        WgPublicKey = protocolExtra?.WgPublicKey ?? string.Empty;
        WgInterfaceAddress = protocolExtra?.WgInterfaceAddress ?? string.Empty;
        WgReserved = protocolExtra?.WgReserved ?? string.Empty;
        WgMtu = protocolExtra?.WgMtu ?? 1280;
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
        SelectedSource.SetProtocolExtra(SelectedSource.GetProtocolExtra() with
        {
            Ports = Ports.NullIfEmpty(),
            AlterId = AlterId > 0 ? AlterId.ToString() : null,
            Flow = Flow.NullIfEmpty(),
            SalamanderPass = SalamanderPass.NullIfEmpty(),
            UpMbps = UpMbps >= 0 ? UpMbps : null,
            DownMbps = DownMbps >= 0 ? DownMbps : null,
            HopInterval = HopInterval.NullIfEmpty(),
            VmessSecurity = VmessSecurity.NullIfEmpty(),
            VlessEncryption = VlessEncryption.NullIfEmpty(),
            SsMethod = SsMethod.NullIfEmpty(),
            WgPublicKey = WgPublicKey.NullIfEmpty(),
            WgInterfaceAddress = WgInterfaceAddress.NullIfEmpty(),
            WgReserved = WgReserved.NullIfEmpty(),
            WgMtu = WgMtu >= 576 ? WgMtu : null,
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

        List<string> shaList = new();
        foreach (var cert in certList)
        {
            var sha = CertPemManager.GetCertSha256Thumbprint(cert);
            if (sha.IsNullOrEmpty())
            {
                return;
            }
            shaList.Add(sha);
        }
        CertSha = string.Join('~', shaList);
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
            serverName = SelectedSource.RequestHost;
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (!Utils.IsDomain(serverName))
        {
            UpdateCertTip(ResUI.ServerNameMustBeValidDomain);
            return;
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
            serverName = SelectedSource.RequestHost;
        }
        if (serverName.IsNullOrEmpty())
        {
            serverName = SelectedSource.Address;
        }
        if (!Utils.IsDomain(serverName))
        {
            UpdateCertTip(ResUI.ServerNameMustBeValidDomain);
            return;
        }
        if (SelectedSource.Port > 0)
        {
            domain += $":{SelectedSource.Port}";
        }

        var (certs, certError) = await CertPemManager.Instance.GetCertChainPemAsync(domain, serverName);
        Cert = CertPemManager.ConcatenatePemChain(certs);
        UpdateCertTip(certError);
    }
}
