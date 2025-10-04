using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;

namespace v2rayN.Views;

public partial class RoutingRuleDetailsWindow
{
    public RoutingRuleDetailsWindow(RulesItem rulesItem)
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;
        this.Loaded += Window_Loaded;
        clbProtocol.SelectionChanged += ClbProtocol_SelectionChanged;
        clbInboundTag.SelectionChanged += ClbInboundTag_SelectionChanged;

        ViewModel = new RoutingRuleDetailsViewModel(rulesItem, UpdateViewHandler);

        cmbOutboundTag.ItemsSource = Global.OutboundTags;
        clbProtocol.ItemsSource = Global.RuleProtocols;
        clbInboundTag.ItemsSource = Global.InboundTags;
        cmbNetwork.ItemsSource = Global.RuleNetworks;

        if (!rulesItem.Id.IsNullOrEmpty())
        {
            rulesItem.Protocol?.ForEach(it =>
            {
                clbProtocol.SelectedItems.Add(it);
            });
            rulesItem.InboundTag?.ForEach(it =>
            {
                clbInboundTag.SelectedItems.Add(it);
            });
        }

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.OutboundTag, v => v.cmbOutboundTag.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Domain, v => v.txtDomain.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IP, v => v.txtIP.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Process, v => v.txtProcess.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoSort, v => v.chkAutoSort.IsChecked).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void ClbProtocol_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ProtocolItems = clbProtocol.SelectedItems.Cast<string>().ToList();
        }
    }

    private void ClbInboundTag_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.InboundTagItems = clbInboundTag.SelectedItems.Cast<string>().ToList();
        }
    }

    private void linkRuleobjectDoc_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://xtls.github.io/config/routing.html#ruleobject");
    }

    private async void BtnSelectProfile_Click(object sender, RoutedEventArgs e)
    {
        var selectWindow = new ProfilesSelectWindow();
        selectWindow.SetConfigTypeFilter(new[] { EConfigType.Custom }, exclude: true);
        if (selectWindow.ShowDialog() == true)
        {
            var profile = await selectWindow.ProfileItem;
            if (profile != null)
            {
                cmbOutboundTag.Text = profile.Remarks;
            }
        }
    }
}
