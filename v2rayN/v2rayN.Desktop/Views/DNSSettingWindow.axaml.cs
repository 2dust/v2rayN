using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class DNSSettingWindow : WindowBase<DNSSettingViewModel>
{
    private static Config _config;

    public DNSSettingWindow()
    {
        InitializeComponent();

        _config = AppManager.Instance.Config;
        Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => Close();
        ViewModel = new DNSSettingViewModel(UpdateViewHandler);

        cmbDirectDNSStrategy.ItemsSource = Global.DomainStrategy;
        cmbRemoteDNSStrategy.ItemsSource = Global.DomainStrategy;
        cmbDirectDNS.ItemsSource = Global.DomainDirectDNSAddress;
        cmbRemoteDNS.ItemsSource = Global.DomainRemoteDNSAddress;
        cmbBootstrapDNS.ItemsSource = Global.DomainPureIPDNSAddress;
        cmbDirectExpectedIPs.ItemsSource = Global.ExpectedIPs;

        cmbdomainStrategy4FreedomCompatible.ItemsSource = Global.DomainStrategy;
        cmbdomainStrategy4OutCompatible.ItemsSource = Global.DomainStrategies4Sbox;
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
            this.Bind(ViewModel, vm => vm.BootstrapDNS, v => v.cmbBootstrapDNS.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Strategy4Freedom, v => v.cmbDirectDNSStrategy.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Strategy4Proxy, v => v.cmbRemoteDNSStrategy.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Hosts, v => v.txtHosts.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DirectExpectedIPs, v => v.cmbDirectExpectedIPs.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ParallelQuery, v => v.togParallelQuery.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ServeStale, v => v.togServeStale.IsChecked).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.RayCustomDNSEnableCompatible, v => v.togRayCustomDNSEnableCompatible.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SBCustomDNSEnableCompatible, v => v.togSBCustomDNSEnableCompatible.IsChecked).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.UseSystemHostsCompatible, v => v.togUseSystemHostsCompatible.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainStrategy4FreedomCompatible, v => v.cmbdomainStrategy4FreedomCompatible.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainDNSAddressCompatible, v => v.cmbdomainDNSAddressCompatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.NormalDNSCompatible, v => v.txtnormalDNSCompatible.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DomainStrategy4Freedom2Compatible, v => v.cmbdomainStrategy4OutCompatible.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainDNSAddress2Compatible, v => v.cmbdomainDNSAddress2Compatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.NormalDNS2Compatible, v => v.txtnormalDNS2Compatible.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TunDNS2Compatible, v => v.txttunDNS2Compatible.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ImportDefConfig4V2rayCompatibleCmd, v => v.btnImportDefConfig4V2rayCompatible).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ImportDefConfig4SingboxCompatibleCmd, v => v.btnImportDefConfig4SingboxCompatible).DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.IsSimpleDNSEnabled)
                .Select(b => !b)
                .BindTo(this.FindControl<TextBlock>("txtBasicDNSSettingsInvalid"), t => t.IsVisible);
            this.WhenAnyValue(x => x.ViewModel.IsSimpleDNSEnabled)
                .Select(b => !b)
                .BindTo(this.FindControl<TextBlock>("txtAdvancedDNSSettingsInvalid"), t => t.IsVisible);
            this.Bind(ViewModel, vm => vm.IsSimpleDNSEnabled, v => v.gridBasicDNSSettings.IsEnabled).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IsSimpleDNSEnabled, v => v.gridAdvancedDNSSettings.IsEnabled).DisposeWith(disposables);
        });
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

    private void linkDnsObjectDoc_Click(object? sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://xtls.github.io/config/dns.html#dnsobject");
    }

    private void linkDnsSingboxObjectDoc_Click(object? sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/dns/");
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        btnCancel.Focus();
    }
}
