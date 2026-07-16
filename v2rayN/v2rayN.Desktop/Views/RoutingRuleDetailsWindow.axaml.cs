using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class RoutingRuleDetailsWindow : WindowBase<RoutingRuleDetailsViewModel>
{
    public RoutingRuleDetailsWindow()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => Close();
        clbProtocol.SelectionChanged += ClbProtocol_SelectionChanged;
        clbInboundTag.SelectionChanged += ClbInboundTag_SelectionChanged;

        cmbOutboundTag.ItemsSource = Global.OutboundTags;
        clbProtocol.ItemsSource = Global.RuleProtocols;
        clbInboundTag.ItemsSource = Global.InboundTags;
        cmbNetwork.ItemsSource = Global.RuleNetworks;
        cmbRuleType.ItemsSource = Utils.GetEnumNames<ERuleType>().AppendEmpty();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.OutboundTag, v => v.cmbOutboundTag.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.OutboundTag, v => v.cmbOutboundTag.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Domain, v => v.txtDomain.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IP, v => v.txtIP.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Process, v => v.txtProcess.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoSort, v => v.chkAutoSort.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RuleType, v => v.cmbRuleType.SelectedValue).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SelectProfileCmd, v => v.btnSelectProfile).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.SelectedSource)
                .WhereNotNull()
                .Subscribe(InitializeData)
                .DisposeWith(disposables);
        });
    }

    private void InitializeData(RulesItem rulesItem)
    {
        if (rulesItem.Id.IsNullOrEmpty())
        {
            return;
        }
        rulesItem.Protocol?.ForEach(it =>
        {
            clbProtocol?.SelectedItems?.Add(it);
        });
        rulesItem.InboundTag?.ForEach(it =>
        {
            clbInboundTag?.SelectedItems?.Add(it);
        });
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void ClbProtocol_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ProtocolItems = clbProtocol.SelectedItems.Cast<string>().ToList();
        }
    }

    private void ClbInboundTag_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.InboundTagItems = clbInboundTag.SelectedItems.Cast<string>().ToList();
        }
    }

    private void linkRuleobjectDoc_Click(object? sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://xtls.github.io/config/routing.html#ruleobject");
    }
}
