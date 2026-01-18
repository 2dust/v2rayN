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
        cmbStreamSecurity.SelectionChanged += CmbStreamSecurity_SelectionChanged;
        btnGUID.Click += btnGUID_Click;
        btnGUID5.Click += btnGUID_Click;

        ViewModel = new AddServerViewModel(profileItem, UpdateViewHandler);

        cmbCoreType.ItemsSource = AppConfig.CoreTypes.AppendEmpty();
        cmbNetwork.ItemsSource = AppConfig.Networks;
        cmbFingerprint.ItemsSource = AppConfig.Fingerprints;
        cmbFingerprint2.ItemsSource = AppConfig.Fingerprints;
        cmbAllowInsecure.ItemsSource = AppConfig.AllowInsecure;
        cmbAlpn.ItemsSource = AppConfig.Alpns;
        cmbEchForceQuery.ItemsSource = AppConfig.EchForceQuerys;

        var lstStreamSecurity = new List<string>();
        lstStreamSecurity.Add(string.Empty);
        lstStreamSecurity.Add(AppConfig.StreamSecurity);

        switch (profileItem.ConfigType)
        {
            case EConfigType.VMess:
                gridVMess.Visibility = Visibility.Visible;
                cmbSecurity.ItemsSource = AppConfig.VmessSecurities;
                if (profileItem.Security.IsNullOrEmpty())
                {
                    profileItem.Security = AppConfig.DefaultSecurity;
                }
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
                lstStreamSecurity.Add(AppConfig.StreamSecurityReality);
                cmbFlow5.ItemsSource = AppConfig.Flows;
                if (profileItem.Security.IsNullOrEmpty())
                {
                    profileItem.Security = AppConfig.None;
                }
                break;

            case EConfigType.Trojan:
                gridTrojan.Visibility = Visibility.Visible;
                lstStreamSecurity.Add(AppConfig.StreamSecurityReality);
                cmbFlow6.ItemsSource = AppConfig.Flows;
                break;

            case EConfigType.Hysteria2:
                gridHysteria2.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbFingerprint.IsEnabled = false;
                cmbFingerprint.Text = string.Empty;
                break;

            case EConfigType.TUIC:
                gridTuic.Visibility = Visibility.Visible;
                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                cmbCoreType.IsEnabled = false;
                cmbFingerprint.IsEnabled = false;
                cmbFingerprint.Text = string.Empty;

                cmbHeaderType8.ItemsSource = AppConfig.TuicCongestionControls;
                break;

            case EConfigType.WireGuard:
                gridWireguard.Visibility = Visibility.Visible;

                sepa2.Visibility = Visibility.Collapsed;
                gridTransport.Visibility = Visibility.Collapsed;
                gridTls.Visibility = Visibility.Collapsed;

                break;

            case EConfigType.Anytls:
                gridAnytls.Visibility = Visibility.Visible;
                cmbCoreType.IsEnabled = false;
                lstStreamSecurity.Add(AppConfig.StreamSecurityReality);
                break;
        }
        cmbStreamSecurity.ItemsSource = lstStreamSecurity;

        gridTlsMore.Visibility = Visibility.Hidden;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);

            switch (profileItem.ConfigType)
            {
                case EConfigType.VMess:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.AlterId, v => v.txtAlterId.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Security, v => v.cmbSecurity.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Shadowsocks:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId3.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Security, v => v.cmbSecurity3.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled3.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.SOCKS:
                case EConfigType.HTTP:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId4.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Security, v => v.txtSecurity4.Text).DisposeWith(disposables);
                    break;

                case EConfigType.VLESS:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Flow, v => v.cmbFlow5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Security, v => v.txtSecurity5.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled5.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Trojan:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId6.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Flow, v => v.cmbFlow6.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.MuxEnabled, v => v.togmuxEnabled6.IsChecked).DisposeWith(disposables);
                    break;

                case EConfigType.Hysteria2:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Path, v => v.txtPath7.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Ports, v => v.txtPorts7.Text).DisposeWith(disposables);
                    break;

                case EConfigType.TUIC:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId8.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Security, v => v.txtSecurity8.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.HeaderType, v => v.cmbHeaderType8.Text).DisposeWith(disposables);
                    break;

                case EConfigType.WireGuard:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.PublicKey, v => v.txtPublicKey9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.Path, v => v.txtPath9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.RequestHost, v => v.txtRequestHost9.Text).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.SelectedSource.ShortId, v => v.txtShortId9.Text).DisposeWith(disposables);
                    break;

                case EConfigType.Anytls:
                    this.Bind(ViewModel, vm => vm.SelectedSource.Id, v => v.txtId10.Text).DisposeWith(disposables);
                    break;
            }
            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.HeaderType, v => v.cmbHeaderType.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.RequestHost, v => v.txtRequestHost.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Path, v => v.txtPath.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Extra, v => v.txtExtra.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.StreamSecurity, v => v.cmbStreamSecurity.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.AllowInsecure, v => v.cmbAllowInsecure.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Alpn, v => v.cmbAlpn.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertSha, v => v.txtCertSha256Pinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CertTip, v => v.labCertPinning.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Cert, v => v.txtCert.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.EchConfigList, v => v.txtEchConfigList.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.EchForceQuery, v => v.cmbEchForceQuery.Text).DisposeWith(disposables);

            //reality
            this.Bind(ViewModel, vm => vm.SelectedSource.Sni, v => v.txtSNI2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Fingerprint, v => v.cmbFingerprint2.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.PublicKey, v => v.txtPublicKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.ShortId, v => v.txtShortId.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.SpiderX, v => v.txtSpiderX.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Mldsa65Verify, v => v.txtMldsa65Verify.Text).DisposeWith(disposables);

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
        SetHeaderType();
        SetTips();
    }

    private void CmbStreamSecurity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var security = cmbStreamSecurity.SelectedItem.ToString();
        if (security == AppConfig.StreamSecurityReality)
        {
            gridRealityMore.Visibility = Visibility.Visible;
            gridTlsMore.Visibility = Visibility.Hidden;
        }
        else if (security == AppConfig.StreamSecurity)
        {
            gridRealityMore.Visibility = Visibility.Hidden;
            gridTlsMore.Visibility = Visibility.Visible;
        }
        else
        {
            gridRealityMore.Visibility = Visibility.Hidden;
            gridTlsMore.Visibility = Visibility.Hidden;
        }
    }

    private void btnGUID_Click(object sender, RoutedEventArgs e)
    {
        txtId.Text =
        txtId5.Text = Utils.GetGuid();
    }

    private void SetHeaderType()
    {
        var lstHeaderType = new List<string>();

        var network = cmbNetwork.SelectedItem.ToString();
        if (network.IsNullOrEmpty())
        {
            lstHeaderType.Add(AppConfig.None);
            cmbHeaderType.ItemsSource = lstHeaderType;
            cmbHeaderType.SelectedIndex = 0;
            return;
        }

        if (network == nameof(ETransport.tcp))
        {
            lstHeaderType.Add(AppConfig.None);
            lstHeaderType.Add(AppConfig.TcpHeaderHttp);
        }
        else if (network is nameof(ETransport.kcp) or nameof(ETransport.quic))
        {
            lstHeaderType.Add(AppConfig.None);
            lstHeaderType.AddRange(AppConfig.KcpHeaderTypes);
        }
        else if (network is nameof(ETransport.xhttp))
        {
            lstHeaderType.AddRange(AppConfig.XhttpMode);
        }
        else if (network == nameof(ETransport.grpc))
        {
            lstHeaderType.Add(AppConfig.GrpcGunMode);
            lstHeaderType.Add(AppConfig.GrpcMultiMode);
        }
        else
        {
            lstHeaderType.Add(AppConfig.None);
        }
        cmbHeaderType.ItemsSource = lstHeaderType;
        cmbHeaderType.SelectedIndex = 0;
    }

    private void SetTips()
    {
        var network = cmbNetwork.SelectedItem.ToString();
        if (network.IsNullOrEmpty())
        {
            network = AppConfig.DefaultNetwork;
        }
        labHeaderType.Visibility = Visibility.Visible;
        popExtra.Visibility = Visibility.Hidden;
        tipRequestHost.Text =
        tipPath.Text =
        tipHeaderType.Text = string.Empty;

        switch (network)
        {
            case nameof(ETransport.tcp):
                tipRequestHost.Text = ResUI.TransportRequestHostTip1;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip1;
                break;

            case nameof(ETransport.kcp):
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip2;
                tipPath.Text = ResUI.TransportPathTip5;
                break;

            case nameof(ETransport.ws):
            case nameof(ETransport.httpupgrade):
                tipRequestHost.Text = ResUI.TransportRequestHostTip2;
                tipPath.Text = ResUI.TransportPathTip1;
                break;

            case nameof(ETransport.xhttp):
                tipRequestHost.Text = ResUI.TransportRequestHostTip2;
                tipPath.Text = ResUI.TransportPathTip1;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip5;
                labHeaderType.Visibility = Visibility.Hidden;
                popExtra.Visibility = Visibility.Visible;
                break;

            case nameof(ETransport.h2):
                tipRequestHost.Text = ResUI.TransportRequestHostTip3;
                tipPath.Text = ResUI.TransportPathTip2;
                break;

            case nameof(ETransport.quic):
                tipRequestHost.Text = ResUI.TransportRequestHostTip4;
                tipPath.Text = ResUI.TransportPathTip3;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip3;
                break;

            case nameof(ETransport.grpc):
                tipRequestHost.Text = ResUI.TransportRequestHostTip5;
                tipPath.Text = ResUI.TransportPathTip4;
                tipHeaderType.Text = ResUI.TransportHeaderTypeTip4;
                labHeaderType.Visibility = Visibility.Hidden;
                break;
        }
    }
}
