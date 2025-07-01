using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class RoutingSettingWindow : WindowBase<RoutingSettingViewModel>
{
    private bool _manualClose = false;

    public RoutingSettingWindow()
    {
        InitializeComponent();

        this.Closing += RoutingSettingWindow_Closing;
        btnCancel.Click += (s, e) => this.Close();
        this.KeyDown += RoutingSettingWindow_KeyDown;
        lstRoutings.SelectionChanged += lstRoutings_SelectionChanged;
        lstRoutings.DoubleTapped += LstRoutings_DoubleTapped;
        menuRoutingAdvancedSelectAll.Click += menuRoutingAdvancedSelectAll_Click;

        ViewModel = new RoutingSettingViewModel(UpdateViewHandler);

        Global.DomainStrategies.ForEach(it =>
        {
            cmbdomainStrategy.Items.Add(it);
        });
        Global.DomainMatchers.ForEach(it =>
        {
            cmbdomainMatcher.Items.Add(it);
        });
        Global.DomainStrategies4Singbox.ForEach(it =>
        {
            cmbdomainStrategy4Singbox.Items.Add(it);
        });

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.lstRoutings.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstRoutings.SelectedItem).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DomainStrategy, v => v.cmbdomainStrategy.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainMatcher, v => v.cmbdomainMatcher.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.SelectedValue).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedRemoveCmd, v => v.menuRoutingAdvancedRemove).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedSetDefaultCmd, v => v.menuRoutingAdvancedSetDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules2).DisposeWith(disposables);

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

            case EViewAction.ShowYesNo:
                if (await UI.ShowYesNo(this, ResUI.RemoveRules) != ButtonResult.Yes)
                {
                    return false;
                }
                break;

            case EViewAction.RoutingRuleSettingWindow:
                if (obj is null)
                    return false;
                return await new RoutingRuleSettingWindow((RoutingItem)obj).ShowDialog<bool>(this);
        }
        return await Task.FromResult(true);
    }

    private void RoutingSettingWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            if (e.Key == Key.A)
            {
                lstRoutings.SelectAll();
            }
        }
        else if (e.Key is Key.Enter or Key.Return)
        {
            ViewModel?.RoutingAdvancedSetDefault();
        }
        else if (e.Key == Key.Delete)
        {
            ViewModel?.RoutingAdvancedRemoveAsync();
        }
    }

    private void menuRoutingAdvancedSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        lstRoutings.SelectAll();
    }

    private void lstRoutings_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedSources = lstRoutings.SelectedItems.Cast<RoutingItemModel>().ToList();
        }
    }

    private void LstRoutings_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ViewModel?.RoutingAdvancedEditAsync(false);
    }

    private void linkdomainStrategy_Click(object? sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://xtls.github.io/config/routing.html");
    }

    private void linkdomainStrategy4Singbox_Click(object? sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/shared/listen/#domain_strategy");
    }

    private void btnCancel_Click(object? sender, RoutedEventArgs e)
    {
        _manualClose = true;
        this.Close(ViewModel?.IsModified);
    }

    private void RoutingSettingWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            if (!_manualClose)
            {
                btnCancel_Click(null, null);
            }
        }
    }
}
