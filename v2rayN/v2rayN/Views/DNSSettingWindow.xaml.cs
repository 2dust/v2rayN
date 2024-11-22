using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;

namespace v2rayN.Views
{
    public partial class DNSSettingWindow
    {
        private static Config _config;

        public DNSSettingWindow()
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            _config = AppHandler.Instance.Config;

            ViewModel = new DNSSettingViewModel(UpdateViewHandler);

            Global.DomainStrategy4Freedoms.ForEach(it =>
            {
                cmbdomainStrategy4Freedom.Items.Add(it);
            });
            Global.SingboxDomainStrategy4Out.ForEach(it =>
            {
                cmbdomainStrategy4Out.Items.Add(it);
            });
            Global.DomainDNSAddress.ForEach(it =>
            {
                cmbdomainDNSAddress.Items.Add(it);
            });
            Global.SingboxDomainDNSAddress.ForEach(it =>
            {
                cmbdomainDNSAddress2.Items.Add(it);
            });

            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.useSystemHosts, v => v.togUseSystemHosts.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainStrategy4Freedom, v => v.cmbdomainStrategy4Freedom.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainDNSAddress, v => v.cmbdomainDNSAddress.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.normalDNS, v => v.txtnormalDNS.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.domainStrategy4Freedom2, v => v.cmbdomainStrategy4Out.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainDNSAddress2, v => v.cmbdomainDNSAddress2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.normalDNS2, v => v.txtnormalDNS2.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.tunDNS2, v => v.txttunDNS2.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportDefConfig4V2rayCmd, v => v.btnImportDefConfig4V2ray).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ImportDefConfig4SingboxCmd, v => v.btnImportDefConfig4Singbox).DisposeWith(disposables);
            });
            WindowsUtils.SetDarkBorder(this, AppHandler.Instance.Config.UiItem.FollowSystemTheme ? WindowsUtils.IsDarkTheme() : AppHandler.Instance.Config.UiItem.ColorModeDark);
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.CloseWindow:
                    this.DialogResult = true;
                    break;
            }
            return await Task.FromResult(true);
        }

        private void linkDnsObjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://xtls.github.io/config/dns.html#dnsobject");
        }

        private void linkDnsSingboxObjectDoc_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/dns/");
        }
    }
}