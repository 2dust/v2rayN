using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class RoutingSettingWindow : WindowBase<RoutingSettingViewModel>
{
    public RoutingSettingWindow()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        Closing += RoutingSettingWindow_Closing;
        KeyDown += RoutingSettingWindow_KeyDown;
        lstRoutings.SelectionChanged += lstRoutings_SelectionChanged;
        lstRoutings.DoubleTapped += LstRoutings_DoubleTapped;
        menuRoutingAdvancedSelectAll.Click += menuRoutingAdvancedSelectAll_Click;

        ViewModel = new RoutingSettingViewModel(UpdateViewHandler);

        cmbdomainStrategy.ItemsSource = Global.DomainStrategies;
        cmbdomainStrategy4Singbox.ItemsSource = Global.DomainStrategies4Sbox;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.lstRoutings.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstRoutings.SelectedItem).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DomainStrategy, v => v.cmbdomainStrategy.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.SelectedValue).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedRemoveCmd, v => v.menuRoutingAdvancedRemove).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedSetDefaultCmd, v => v.menuRoutingAdvancedSetDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules2).DisposeWith(disposables);
        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.ShowYesNo:
                if (await UI.ShowYesNo(this, ResUI.RemoveRules) != ButtonResult.Yes)
                {
                    return false;
                }
                break;

            case EViewAction.RoutingRuleSettingWindow:
                if (obj is null)
                {
                    return false;
                }

                return await new RoutingRuleSettingWindow((RoutingItem)obj).ShowDialog<bool>(this);
        }
        return await Task.FromResult(true);
    }

    private bool _closed = false;

    private void RoutingSettingWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_closed)
        {
            return;
        }

        // DomainStrategy is auto-saved reactively; just ensure the caller knows changes were made
        if (ViewModel?.IsModified == true)
        {
            e.Cancel = true;
            _closed = true;
            Close(true);
        }
    }

    private void RoutingSettingWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            switch (e.Key)
            {
                case Key.A:
                    lstRoutings.SelectAll();
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Enter:
                    //case Key.Return:
                    ViewModel?.RoutingAdvancedSetDefault();
                    break;

                case Key.Delete:
                case Key.Back:
                    ViewModel?.RoutingAdvancedRemoveAsync();
                    break;
            }
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
        ProcUtils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/route/rule_action/#strategy");
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
    }
}
