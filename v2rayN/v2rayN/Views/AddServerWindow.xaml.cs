using System.Windows.Controls;

namespace v2rayN.Views;

public partial class AddServerWindow
{
    public AddServerWindow(ProfileItem profileItem)
    {
        InitializeComponent();

        Owner = Application.Current.MainWindow;
        Loaded += Window_Loaded;
        cmbNetwork.SelectionChanged += CmbNetwork_SelectionChanged;
        cmbHeaderTypeRaw.SelectionChanged += CmbHeaderTypeRaw_SelectionChanged;
        cmbStreamSecurity.SelectionChanged += CmbStreamSecurity_SelectionChanged;
        btnGUID.Click += btnGUID_Click;
        btnGUID5.Click += btnGUID_Click;

        ViewModel = new AddServerViewModel(profileItem, UpdateViewHandler);

        cmbCoreType.ItemsSource = Global.CoreTypes.AppendEmpty();
        cmbNetwork.ItemsSource = Global.Networks;
        if (ViewModel.SelectedSource.Network.IsNullOrEmpty() || !Global.Networks.Contains(ViewModel.SelectedSource.Network))
        {
            ViewModel.SelectedSource.Network = Global.DefaultNetwork;
        }

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
                gridVMess.Visibility = Visibility.Visible;
                cmbSecurity.ItemsSource = Global.VmessSecurities;
                break;

            case EConfigType.Shadowsocks:
                gridSs.Visibility = Visibility.Visible;
                cmbSecurity3.ItemsSource = AppManager.Instance.GetShadowsocksSecurities(profileItem);
                break;

            case EConfigType.SOCKS:
            case EConfigType.HTTP:
                gridSocks.Visibility = Visibility.Visible;
                break;

            case EConfigType.VLESS:
                gridVLESS.Visibility = Visibility.Visible;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow5.ItemsSource = Global.Flows;
                break;

            case EConfigType.Trojan:
                gridTrojan.Visibility = Visibility.Visible;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                cmbFlow6.ItemsSource = Global.Flows;
                break;

            case EConfigType.Hysteria2:
                gridHysteria2.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                break;

            case EConfigType.TUIC:
                gridTuic.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                cmbFingerprint.IsEnabled = false;
                gridFinalmask.Visibility = Visibility.Collapsed;

                cmbCongestionControl8.ItemsSource = Global.TuicCongestionControls;
                break;

            case EConfigType.WireGuard:
                gridWireguard.Visibility = Visibility.Visible;

                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                gridTls.Visibility = Visibility.Collapsed;

                break;

            case EConfigType.Anytls:
                gridAnytls.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                lstStreamSecurity.Add(Global.StreamSecurityReality);
                gridFinalmask.Visibility = Visibility.Collapsed;
                break;

            case EConfigType.Naive:
                gridNaive.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                gridFinalmask.Visibility = Visibility.Collapsed;
                cmbFingerprint.IsEnabled = false;
                cmbAlpn.IsEnabled = false;
                cmbAllowInsecure.IsEnabled = false;

                cmbCongestionControl12.ItemsSource = Global.NaiveCongestionControls;
                break;
        }
        cmbStreamSecurity.ItemsSource = lstStreamSecurity;

        gridTlsMore.Visibility = Visibility.Collapsed;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);

            switch (profileItem.ConfigType)
            {
                case EConfigType.VMess:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.AlterId, v => v.txtAlterId.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.VmessSecurity, v => v.cmbSecurity.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Shadowsocks:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId3.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SsMethod, v => v.cmbSecurity3.Text).DisposeWith(disposables);
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
                    this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.VlessEncryption, v => v.txtSecurity5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled5.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Trojan:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId6.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Flow, v => v.cmbFlow6.Text).DisposeWith(disposables);
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
                    this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl8.Text).DisposeWith(disposables);
                    break;

                case EConfigType.WireGuard:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtId9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.WgPublicKey, v => v.txtPublicKey9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.WgPresharedKey, v => v.txtPreSharedKey9.Text).DisposeWith(disposables);
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
                    this.Bind(ViewModel, vm => vm.CongestionControl, v => v.cmbCongestionControl12.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.InsecureConcurrency, v => v.txtInsecureConcurrency12.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.Uot, v => v.togUotEnabled12.IsChecked).DisposeWith(disposables);
                    break;
            }
            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RawHeaderType, v => v.cmbHeaderTypeRaw.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostRaw.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathRaw.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.KcpHeaderType, v => v.cmbHeaderTypeKcp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpSeed, v => v.txtKcpSeed.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KcpMtu, v => v.txtKcpMtu.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostWs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathWs.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostHttpupgrade.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathHttpupgrade.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.XhttpMode, v => v.cmbHeaderTypeXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Host, v => v.txtRequestHostXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path, v => v.txtPathXhttp.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.XhttpExtra, v => v.txtExtraXhttp.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.GrpcMode, v => v.cmbHeaderTypeGrpc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcAuthority, v => v.txtRequestHostGrpc.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GrpcServiceName, v => v.txtPathGrpc.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.StreamSecurity, v => v.cmbStreamSecurity.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.AllowInsecure, v => v.cmbAllowInsecure.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Alpn, v => v.cmbAlpn.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertSha, v => v.txtCertSha256Pinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertTip, v => v.labCertPinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AllowInsecureCertFetch, v => v.togAllowInsecureCertFetch.IsChecked).DisposeWith(disposables);
            this.WhenAnyValue(x => x.ViewModel.AllowInsecureCertFetch)
                .Select(b => b ? Visibility.Visible : Visibility.Collapsed)
                .BindTo(this, v => v.txtAllowInsecureCertFetchTips.Visibility);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.EchConfigList, v => v.txtEchConfigList.Text).DisposeWith(disposables);

            //reality
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint2.Text).DisposeWith(disposables);
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
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                DialogResult = true;
                break;
        }
        return await Task.FromResult(true);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void CmbNetwork_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetTransportGridVisibility();
    }

    private void CmbHeaderTypeRaw_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetRawHttpFieldsVisibility();
    }

    private void CmbStreamSecurity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var security = cmbStreamSecurity.SelectedItem.ToString();
        if (security == Global.StreamSecurityReality)
        {
            gridRealityMore.Visibility = Visibility.Visible;
            gridTlsMore.Visibility = Visibility.Collapsed;
        }
        else if (security == Global.StreamSecurity)
        {
            gridRealityMore.Visibility = Visibility.Collapsed;
            gridTlsMore.Visibility = Visibility.Visible;
        }
        else
        {
            gridRealityMore.Visibility = Visibility.Collapsed;
            gridTlsMore.Visibility = Visibility.Collapsed;
        }
    }

    private void btnGUID_Click(object sender, RoutedEventArgs e)
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

        gridTransportRaw.Visibility = Visibility.Collapsed;
        gridTransportKcp.Visibility = Visibility.Collapsed;
        gridTransportWs.Visibility = Visibility.Collapsed;
        gridTransportHttpupgrade.Visibility = Visibility.Collapsed;
        gridTransportXhttp.Visibility = Visibility.Collapsed;
        gridTransportGrpc.Visibility = Visibility.Collapsed;

        switch (network)
        {
            case nameof(ETransport.raw):
                gridTransportRaw.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.kcp):
                gridTransportKcp.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.ws):
                gridTransportWs.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.httpupgrade):
                gridTransportHttpupgrade.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.xhttp):
                gridTransportXhttp.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.grpc):
                gridTransportGrpc.Visibility = Visibility.Visible;
                break;

            default:
                gridTransportRaw.Visibility = Visibility.Visible;
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
        gridTransportRawHttp.Visibility = showRawHttpFields
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
