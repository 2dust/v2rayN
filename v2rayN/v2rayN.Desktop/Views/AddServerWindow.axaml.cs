using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class AddServerWindow : WindowBase<AddServerViewModel>
{
    public AddServerWindow()
    {
        InitializeComponent();
    }

    public AddServerWindow(ProfileItem profileItem)
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => Close();
        cmbNetwork.SelectionChanged += CmbNetwork_SelectionChanged;
        cmbHeaderTypeRaw.SelectionChanged += CmbHeaderTypeRaw_SelectionChanged;
        cmbStreamSecurity.SelectionChanged += CmbStreamSecurity_SelectionChanged;
        btnGUID.Click += btnGUID_Click;
        btnGUID5.Click += btnGUID_Click;

        ViewModel = new AddServerViewModel(profileItem, UpdateViewHandler);

        cmbCoreType.ItemsSource = Global.CoreTypes.AppendEmpty();
        cmbNetwork.ItemsSource = Global.Networks;

        cmbHeaderTypeRaw.ItemsSource = new List<string> { Global.None, Global.RawHeaderHttp };

        var kcpHeaderTypes = new List<string> { Global.None };
        kcpHeaderTypes.AddRange(Global.KcpHeaderTypes);
        cmbHeaderTypeKcp.ItemsSource = kcpHeaderTypes;

        cmbHeaderTypeXhttp.ItemsSource = Global.XhttpMode;
        cmbHeaderTypeGrpc.ItemsSource = new List<string> { Global.GrpcGunMode, Global.GrpcMultiMode };

        cmbFingerprint.ItemsSource = Global.Fingerprints;
        cmbFingerprint2.ItemsSource = Global.Fingerprints;
        cmbAllowInsecure.ItemsSource = Global.AllowInsecure;
        cmbAlpn.ItemsSource = Global.Alpns;

        var lstStreamSecurity = new List<string> { string.Empty, Global.StreamSecurity };

        switch (profileItem.ConfigType)
        {
            case EConfigType.VMess:
                gridVMess.IsVisible = true;
                cmbSecurity.ItemsSource = Global.VmessSecurities;
                break;

            case EConfigType.Shadowsocks:
                gridSs.IsVisible = true;
                cmbSecurity3.ItemsSource = AppManager.Instance.GetShadowsocksSecurities(profileItem);
                break;

            case EConfigType.SOCKS:
            case EConfigType.HTTP:
                gridSocks.IsVisible = true;
                break;

            case EConfigType.VLESS:
                gridVLESS.IsVisible = true;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow5.ItemsSource = Global.Flows;
                break;

            case EConfigType.Trojan:
                gridTrojan.IsVisible = true;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow6.ItemsSource = Global.Flows;
                break;

            case EConfigType.Hysteria2:
                gridHysteria2.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbFingerprint.IsEnabled = false;
                cmbFingerprint.SelectedValue = string.Empty;
                break;

            case EConfigType.TUIC:
                gridTuic.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbCoreType.IsEnabled = false;
                cmbFingerprint.IsEnabled = false;
                cmbFingerprint.SelectedValue = string.Empty;
                gridFinalmask.IsVisible = false;

                cmbCongestionControl8.ItemsSource = Global.TuicCongestionControls;
                break;

            case EConfigType.WireGuard:
                gridWireguard.IsVisible = true;

                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                gridTls.IsVisible = false;

                break;

            case EConfigType.Anytls:
                gridAnytls.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbCoreType.IsEnabled = false;
                gridFinalmask.IsVisible = false;
                break;

            case EConfigType.Naive:
                gridNaive.IsVisible = true;
                sepa2.IsVisible = false;
                gridTransport.IsVisible = false;
                cmbCoreType.IsEnabled = false;
                gridFinalmask.IsVisible = false;
                cmbFingerprint.IsEnabled = false;
                cmbFingerprint.SelectedValue = string.Empty;
                cmbAlpn.IsEnabled = false;
                cmbAlpn.SelectedValue = string.Empty;
                cmbAllowInsecure.IsEnabled = false;
                cmbAllowInsecure.SelectedValue = string.Empty;

                cmbCongestionControl12.ItemsSource = Global.NaiveCongestionControls;
                break;
        }
        cmbStreamSecurity.ItemsSource = lstStreamSecurity;

        gridTlsMore.IsVisible = false;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);

            switch (profileItem.ConfigType)
            {
                case EConfigType.VMess:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.AlterId, v => v.txtAlterId.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.VmessSecurity, v => v.cmbSecurity.SelectedValue).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Shadowsocks:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId3.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SsMethod, v => v.cmbSecurity3.SelectedValue).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Uot, v => v.togUotEnabled3.IsChecked).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled3.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.SOCKS:
                case EConfigType.HTTP:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId4.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtSecurity4.Text).DisposeWith(disposables);
                    break;

                case EConfigType.VLESS:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow5.SelectedValue).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.VlessEncryption, v => v.txtSecurity5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled5.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Trojan:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId6.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow6.SelectedValue).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled6.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Hysteria2:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SalamanderPass, v => v.txtPath7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Ports, v => v.txtPorts7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.HopInterval, v => v.txtHopInt7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.UpMbps, v => v.txtUpMbps7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.DownMbps, v => v.txtDownMbps7.Text).DisposeWith(disposables);
                    break;

                case EConfigType.TUIC:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtId8.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtSecurity8.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl8.SelectedValue).DisposeWith(disposables);
                    break;

                case EConfigType.WireGuard:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.WgPublicKey, v => v.txtPublicKey9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.WgReserved, v => v.txtPath9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.WgInterfaceAddress, v => v.txtRequestHost9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.WgMtu, v => v.txtShortId9.Text).DisposeWith(disposables);
                    break;

                case EConfigType.Anytls:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId11.Text).DisposeWith(disposables);
                    break;

                case EConfigType.Naive:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Username, v => v.txtId12.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtSecurity12.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.NaiveQuic, v => v.togNaiveQuic12.IsChecked).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.NaiveQuic, v => v.cmbCongestionControl12.IsEnabled).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl12.SelectedValue).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.InsecureConcurrency, v => v.txtInsecureConcurrency12.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Uot, v => v.togUotEnabled12.IsChecked).DisposeWith(disposables);
                    break;
            }
            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RawHeaderType, v => v.cmbHeaderTypeRaw.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostRaw.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathRaw.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.KcpHeaderType, v => v.cmbHeaderTypeKcp.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpSeed, v => v.txtKcpSeed.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpMtu, v => v.txtKcpMtu.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostWs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathWs.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostHttpupgrade.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathHttpupgrade.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.XhttpMode, v => v.cmbHeaderTypeXhttp.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.XhttpExtra, v => v.txtExtraXhttp.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.GrpcMode, v => v.cmbHeaderTypeGrpc.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcAuthority, v => v.txtRequestHostGrpc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcServiceName, v => v.txtPathGrpc.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.StreamSecurity, v => v.cmbStreamSecurity.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.AllowInsecure, v => v.cmbAllowInsecure.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Alpn, v => v.cmbAlpn.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertSha, v => v.txtCertSha256Pinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertTip, v => v.labCertPinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AllowInsecureCertFetch, v => v.togAllowInsecureCertFetch.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AllowInsecureCertFetch, v => v.txtAllowInsecureCertFetchTips.IsVisible).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.EchConfigList, v => v.txtEchConfigList.Text).DisposeWith(disposables);

            //reality
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint2.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PublicKey, v => v.txtPublicKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.ShortId, v => v.txtShortId.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.SpiderX, v => v.txtSpiderX.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Mldsa65Verify, v => v.txtMldsa65Verify.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Finalmask, v => v.txtFinalmask.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.FetchCertCmd, v => v.btnFetchCert).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FetchCertChainCmd, v => v.btnFetchCertChain).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });

        Title = $"{profileItem.ConfigType}";
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                Close(true);
                break;
        }
        return await Task.FromResult(true);
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void CmbNetwork_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetTransportGridVisibility();
    }

    private void CmbHeaderTypeRaw_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetRawHttpFieldsVisibility();
    }

    private void CmbStreamSecurity_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var security = cmbStreamSecurity.SelectedItem.ToString();
        if (security == Global.StreamSecurityReality)
        {
            gridRealityMore.IsVisible = true;
            gridTlsMore.IsVisible = false;
        }
        else if (security == Global.StreamSecurity)
        {
            gridRealityMore.IsVisible = false;
            gridTlsMore.IsVisible = true;
        }
        else
        {
            gridRealityMore.IsVisible = false;
            gridTlsMore.IsVisible = false;
        }
    }

    private void btnGUID_Click(object? sender, RoutedEventArgs e)
    {
        txtId.Text =
        txtId5.Text = Utils.GetGuid();
    }

    private void SetTransportGridVisibility()
    {
        var network = cmbNetwork.SelectedItem?.ToString();
        if (network.IsNullOrEmpty())
        {
            network = Global.DefaultNetwork;
        }

        gridTransportRaw.IsVisible = false;
        gridTransportKcp.IsVisible = false;
        gridTransportWs.IsVisible = false;
        gridTransportHttpupgrade.IsVisible = false;
        gridTransportXhttp.IsVisible = false;
        gridTransportGrpc.IsVisible = false;

        switch (network)
        {
            case nameof(ETransport.raw):
                gridTransportRaw.IsVisible = true;
                break;

            case nameof(ETransport.kcp):
                gridTransportKcp.IsVisible = true;
                break;

            case nameof(ETransport.ws):
                gridTransportWs.IsVisible = true;
                break;

            case nameof(ETransport.httpupgrade):
                gridTransportHttpupgrade.IsVisible = true;
                break;

            case nameof(ETransport.xhttp):
                gridTransportXhttp.IsVisible = true;
                break;

            case nameof(ETransport.grpc):
                gridTransportGrpc.IsVisible = true;
                break;

            default:
                gridTransportRaw.IsVisible = true;
                break;
        }

        SetRawHttpFieldsVisibility();
    }

    private void SetRawHttpFieldsVisibility()
    {
        var network = cmbNetwork.SelectedItem?.ToString();
        if (network.IsNullOrEmpty())
        {
            network = Global.DefaultNetwork;
        }

        var rawHeaderType = cmbHeaderTypeRaw.SelectedItem?.ToString();
        var showRawHttpFields = network == nameof(ETransport.raw)
                                && rawHeaderType == Global.RawHeaderHttp;
        gridTransportRawHttp.IsVisible = showRawHttpFields;
    }
}
