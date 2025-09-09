using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class RoutingRuleDetailsWindow : WindowBase<RoutingRuleDetailsViewModel>
{
    public RoutingRuleDetailsWindow()
    {
        InitializeComponent();
    }

    public RoutingRuleDetailsWindow(RulesItem rulesItem)
    {
        InitializeComponent();

        this.Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => this.Close();
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
                clbProtocol?.SelectedItems?.Add(it);
            });
            rulesItem.InboundTag?.ForEach(it =>
            {
                clbInboundTag?.SelectedItems?.Add(it);
            });
        }

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.OutboundTag, v => v.cmbOutboundTag.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Port, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Network, v => v.cmbNetwork.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Enabled, v => v.togEnabled.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Domain, v => v.txtDomain.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IP, v => v.txtIP.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Process, v => v.txtProcess.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AutoSort, v => v.chkAutoSort.IsChecked).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                this.Close(true);
                break;
        }
        return await Task.FromResult(true);
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

    private async void BtnSelectProfile_Click(object? sender, RoutedEventArgs e)
    {
        var selectWindow = new ProfilesSelectWindow();
        selectWindow.SetConfigTypeFilter(new[] { EConfigType.Custom }, exclude: true);
        var result = await selectWindow.ShowDialog<bool?>(this);
        if (result == true)
        {
            var profile = await selectWindow.ProfileItem;
            if (profile != null)
            {
                cmbOutboundTag.Text = profile.Remarks;
            }
        }
    }
}
