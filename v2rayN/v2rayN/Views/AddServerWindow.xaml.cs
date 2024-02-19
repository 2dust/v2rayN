using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using v2rayN.Handler;
using v2rayN.Model;
using v2rayN.Resx;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class AddServerWindow
    {
        public AddServerWindow(ProfileItem profileItem)
        {
            InitializeComponent();

            // 设置窗口的尺寸不大于屏幕的尺寸
            if (this.Width > SystemParameters.WorkArea.Width)
            {
                this.Width = SystemParameters.WorkArea.Width;
            }
            if (this.Height > SystemParameters.WorkArea.Height)
            {
                this.Height = SystemParameters.WorkArea.Height;
            }

            this.Owner = Application.Current.MainWindow;
            this.Loaded += Window_Loaded;
            cmbNetwork.SelectionChanged += CmbNetwork_SelectionChanged;
            cmbStreamSecurity.SelectionChanged += CmbStreamSecurity_SelectionChanged;

            ViewModel = new AddServerViewModel(profileItem, this);

            if (profileItem.configType == EConfigType.VLESS)
            {
                Global.CoreTypes4VLESS.ForEach(it =>
                {
                    cmbCoreType.Items.Add(it);
                });
            }
            else
            {
                Global.CoreTypes.ForEach(it =>
                {
                    cmbCoreType.Items.Add(it);
                });
            }
            cmbCoreType.Items.Add(string.Empty);

            cmbStreamSecurity.Items.Add(string.Empty);
            cmbStreamSecurity.Items.Add(Global.StreamSecurity);

            Global.Networks.ForEach(it =>
            {
                cmbNetwork.Items.Add(it);
            });
            Global.Fingerprints.ForEach(it =>
            {
                cmbFingerprint.Items.Add(it);
                cmbFingerprint2.Items.Add(it);
            });
            Global.AllowInsecure.ForEach(it =>
            {
                cmbAllowInsecure.Items.Add(it);
            });
            Global.Alpns.ForEach(it =>
            {
                cmbAlpn.Items.Add(it);
            });

            switch (profileItem.configType)
            {
                case EConfigType.VMess:
                    gridVMess.Visibility = Visibility.Visible;
                    Global.VmessSecurities.ForEach(it =>
                    {
                        cmbSecurity.Items.Add(it);
                    });
                    if (profileItem.security.IsNullOrEmpty())
                    {
                        profileItem.security = Global.DefaultSecurity;
                    }
                    break;

                case EConfigType.Shadowsocks:
                    gridSs.Visibility = Visibility.Visible;
                    LazyConfig.Instance.GetShadowsocksSecurities(profileItem).ForEach(it =>
                    {
                        cmbSecurity3.Items.Add(it);
                    });
                    break;

                case EConfigType.Socks:
                    gridSocks.Visibility = Visibility.Visible;
                    break;

                case EConfigType.VLESS:
                    gridVLESS.Visibility = Visibility.Visible;
                    cmbStreamSecurity.Items.Add(Global.StreamSecurityReality);
                    Global.Flows.ForEach(it =>
                    {
                        cmbFlow5.Items.Add(it);
                    });
                    if (profileItem.security.IsNullOrEmpty())
                    {
                        profileItem.security = Global.None;
                    }
                    break;

                case EConfigType.Trojan:
                    gridTrojan.Visibility = Visibility.Visible;
                    cmbStreamSecurity.Items.Add(Global.StreamSecurityReality);
                    Global.Flows.ForEach(it =>
                    {
                        cmbFlow6.Items.Add(it);
                    });
                    break;

                case EConfigType.Hysteria2:
                    gridHysteria2.Visibility = Visibility.Visible;
                    sepa2.Visibility = Visibility.Collapsed;
                    gridTransport.Visibility = Visibility.Collapsed;
                    cmbCoreType.IsEnabled = false;
                    cmbFingerprint.IsEnabled = false;
                    cmbFingerprint.Text = string.Empty;
                    break;

                case EConfigType.Tuic:
                    gridTuic.Visibility = Visibility.Visible;
                    sepa2.Visibility = Visibility.Collapsed;
                    gridTransport.Visibility = Visibility.Collapsed;
                    cmbCoreType.IsEnabled = false;
                    cmbFingerprint.IsEnabled = false;
                    cmbFingerprint.Text = string.Empty;

                    Global.TuicCongestionControls.ForEach(it =>
                    {
                        cmbHeaderType8.Items.Add(it);
                    });
                    break;

                case EConfigType.Wireguard:
                    gridWireguard.Visibility = Visibility.Visible;

                    sepa2.Visibility = Visibility.Collapsed;
                    gridTransport.Visibility = Visibility.Collapsed;
                    gridTls.Visibility = Visibility.Collapsed;
                    cmbCoreType.IsEnabled = false;

                    break;
            }

            gridTlsMore.Visibility = Visibility.Hidden;

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.SelectedSource.coreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.address, v => v.txtAddress.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.port, v => v.txtPort.Text).DisposeWith(disposables);

                switch (profileItem.configType)
                {
                    case EConfigType.VMess:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.alterId, v => v.txtAlterId.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.security, v => v.cmbSecurity.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.Shadowsocks:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId3.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.security, v => v.cmbSecurity3.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.Socks:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId4.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.security, v => v.txtSecurity4.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.VLESS:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId5.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.flow, v => v.cmbFlow5.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.security, v => v.txtSecurity5.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.Trojan:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId6.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.flow, v => v.cmbFlow6.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.Hysteria2:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId7.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.path, v => v.txtPath7.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.Tuic:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId8.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.security, v => v.txtSecurity8.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.headerType, v => v.cmbHeaderType8.Text).DisposeWith(disposables);
                        break;

                    case EConfigType.Wireguard:
                        this.Bind(ViewModel, vm => vm.SelectedSource.id, v => v.txtId9.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.publicKey, v => v.txtPublicKey9.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.path, v => v.txtPath9.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.requestHost, v => v.txtRequestHost9.Text).DisposeWith(disposables);
                        this.Bind(ViewModel, vm => vm.SelectedSource.shortId, v => v.txtShortId9.Text).DisposeWith(disposables);
                        break;
                }
                this.Bind(ViewModel, vm => vm.SelectedSource.network, v => v.cmbNetwork.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.headerType, v => v.cmbHeaderType.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.requestHost, v => v.txtRequestHost.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.path, v => v.txtPath.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SelectedSource.streamSecurity, v => v.cmbStreamSecurity.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.sni, v => v.txtSNI.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.allowInsecure, v => v.cmbAllowInsecure.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.fingerprint, v => v.cmbFingerprint.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.alpn, v => v.cmbAlpn.Text).DisposeWith(disposables);
                //reality
                this.Bind(ViewModel, vm => vm.SelectedSource.sni, v => v.txtSNI2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.fingerprint, v => v.cmbFingerprint2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.publicKey, v => v.txtPublicKey.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.shortId, v => v.txtShortId.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.spiderX, v => v.txtSpiderX.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });

            this.Title = $"{profileItem.configType}";
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
            if (security == Global.StreamSecurityReality)
            {
                gridRealityMore.Visibility = Visibility.Visible;
                gridTlsMore.Visibility = Visibility.Hidden;
            }
            else if (security == Global.StreamSecurity)
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
            txtId5.Text = Utile.GetGUID();
        }

        private void SetHeaderType()
        {
            cmbHeaderType.Items.Clear();

            var network = cmbNetwork.SelectedItem.ToString();
            if (Utile.IsNullOrEmpty(network))
            {
                cmbHeaderType.Items.Add(Global.None);
                return;
            }

            if (network == Global.DefaultNetwork)
            {
                cmbHeaderType.Items.Add(Global.None);
                cmbHeaderType.Items.Add(Global.TcpHeaderHttp);
            }
            else if (network is "kcp" or "quic")
            {
                cmbHeaderType.Items.Add(Global.None);
                Global.KcpHeaderTypes.ForEach(it =>
                {
                    cmbHeaderType.Items.Add(it);
                });
            }
            else if (network == "grpc")
            {
                cmbHeaderType.Items.Add(Global.GrpcGunMode);
                cmbHeaderType.Items.Add(Global.GrpcMultiMode);
            }
            else
            {
                cmbHeaderType.Items.Add(Global.None);
            }
            cmbHeaderType.SelectedIndex = 0;
        }

        private void SetTips()
        {
            var network = cmbNetwork.SelectedItem.ToString();
            if (Utile.IsNullOrEmpty(network))
            {
                network = Global.DefaultNetwork;
            }
            labHeaderType.Visibility = Visibility.Visible;
            tipRequestHost.Text =
            tipPath.Text =
            tipHeaderType.Text = string.Empty;

            switch (network)
            {
                case Global.DefaultNetwork:
                    tipRequestHost.Text = ResUI.TransportRequestHostTip1;
                    tipHeaderType.Text = ResUI.TransportHeaderTypeTip1;
                    break;

                case "kcp":
                    tipHeaderType.Text = ResUI.TransportHeaderTypeTip2;
                    tipPath.Text = ResUI.TransportPathTip5;
                    break;

                case "ws":
                    tipRequestHost.Text = ResUI.TransportRequestHostTip2;
                    tipPath.Text = ResUI.TransportPathTip1;
                    break;

                case "h2":
                    tipRequestHost.Text = ResUI.TransportRequestHostTip3;
                    tipPath.Text = ResUI.TransportPathTip2;
                    break;

                case "quic":
                    tipRequestHost.Text = ResUI.TransportRequestHostTip4;
                    tipPath.Text = ResUI.TransportPathTip3;
                    tipHeaderType.Text = ResUI.TransportHeaderTypeTip3;
                    break;

                case "grpc":
                    tipPath.Text = ResUI.TransportPathTip4;
                    tipHeaderType.Text = ResUI.TransportHeaderTypeTip4;
                    labHeaderType.Visibility = Visibility.Hidden;
                    break;
            }
        }
    }
}