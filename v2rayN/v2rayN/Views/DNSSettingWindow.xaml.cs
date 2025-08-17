using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;
using ServiceLib.Manager;

namespace v2rayN.Views;

public partial class DNSSettingWindow
{
    private static Config _config;

    public DNSSettingWindow()
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;
        _config = AppHandler.Instance.Config;

        ViewModel = new DNSSettingViewModel(UpdateViewHandler);

        cmbRayFreedomDNSStrategy.ItemsSource = Global.DomainStrategy4Freedoms;
        cmbSBDirectDNSStrategy.ItemsSource = Global.SingboxDomainStrategy4Out;
        cmbSBRemoteDNSStrategy.ItemsSource = Global.SingboxDomainStrategy4Out;
        cmbDirectDNS.ItemsSource = Global.DomainDirectDNSAddress;
        cmbSBResolverDNS.ItemsSource = Global.DomainDirectDNSAddress.Concat(new[] { "dhcp://auto,localhost" });
        cmbRemoteDNS.ItemsSource = Global.DomainRemoteDNSAddress;
        cmbSBFinalResolverDNS.ItemsSource = Global.DomainPureIPDNSAddress.Concat(new[] { "dhcp://auto,localhost" });
        cmbDirectExpectedIPs.ItemsSource = Global.ExpectedIPs;

        cmbdomainStrategy4FreedomCompatible.ItemsSource = Global.DomainStrategy4Freedoms;
        cmbdomainStrategy4OutCompatible.ItemsSource = Global.SingboxDomainStrategy4Out;
        cmbdomainDNSAddressCompatible.ItemsSource = Global.DomainPureIPDNSAddress;
        cmbdomainDNSAddress2Compatible.ItemsSource = Global.DomainPureIPDNSAddress;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.UseSystemHosts, v => v.togUseSystemHosts.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AddCommonHosts, v => v.togAddCommonHosts.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.FakeIP, v => v.togFakeIP.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.BlockBindingQuery, v => v.togBlockBindingQuery.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DirectDNS, v => v.cmbDirectDNS.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RemoteDNS, v => v.cmbRemoteDNS.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SingboxOutboundsResolveDNS, v => v.cmbSBResolverDNS.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SingboxFinalResolveDNS, v => v.cmbSBFinalResolverDNS.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RayStrategy4Freedom, v => v.cmbRayFreedomDNSStrategy.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SingboxStrategy4Direct, v => v.cmbSBDirectDNSStrategy.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SingboxStrategy4Proxy, v => v.cmbSBRemoteDNSStrategy.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Hosts, v => v.txtHosts.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DirectExpectedIPs, v => v.cmbDirectExpectedIPs.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.RayCustomDNSEnableCompatible, v => v.togRayCustomDNSEnableCompatible.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SBCustomDNSEnableCompatible, v => v.togSBCustomDNSEnableCompatible.IsChecked).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.UseSystemHostsCompatible, v => v.togUseSystemHostsCompatible.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainStrategy4FreedomCompatible, v => v.cmbdomainStrategy4FreedomCompatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainDNSAddressCompatible, v => v.cmbdomainDNSAddressCompatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.NormalDNSCompatible, v => v.txtnormalDNSCompatible.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DomainStrategy4Freedom2Compatible, v => v.cmbdomainStrategy4OutCompatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainDNSAddress2Compatible, v => v.cmbdomainDNSAddress2Compatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.NormalDNS2Compatible, v => v.txtnormalDNS2Compatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TunDNS2Compatible, v => v.txttunDNS2Compatible.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ImportDefConfig4V2rayCompatibleCmd, v => v.btnImportDefConfig4V2rayCompatible).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ImportDefConfig4SingboxCompatibleCmd, v => v.btnImportDefConfig4SingboxCompatible).DisposeWith(disposables);

            this.WhenAnyValue(
                    x => x.ViewModel.RayCustomDNSEnableCompatible,
                    x => x.ViewModel.SBCustomDNSEnableCompatible,
                    (ray, sb) => ray && sb ? Visibility.Visible : Visibility.Collapsed)
                .BindTo(this, x => x.txtBasicDNSSettingsInvalid.Visibility)
                .DisposeWith(disposables);
            this.WhenAnyValue(
                    x => x.ViewModel.RayCustomDNSEnableCompatible,
                    x => x.ViewModel.SBCustomDNSEnableCompatible,
                    (ray, sb) => ray && sb ? Visibility.Visible : Visibility.Collapsed)
                .BindTo(this, x => x.txtAdvancedDNSSettingsInvalid.Visibility)
                .DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppHandler.Instance.Config.UiItem.CurrentTheme);
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
        ProcUtils.ProcessStart("https://xtls.github.io/config/dns.html#dnsobject");
    }

    private void linkDnsSingboxObjectDoc_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/dns/");
    }
}
