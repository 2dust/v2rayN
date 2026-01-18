namespace v2rayN.Views;

public partial class RoutingSettingWindow
{
    public RoutingSettingWindow()
    {
        InitializeComponent();

        Owner = Application.Current.MainWindow;
        Closing += RoutingSettingWindow_Closing;
        PreviewKeyDown += RoutingSettingWindow_PreviewKeyDown;
        lstRoutings.SelectionChanged += LstRoutings_SelectionChanged;
        lstRoutings.MouseDoubleClick += LstRoutings_MouseDoubleClick;
        menuRoutingAdvancedSelectAll.Click += MenuRoutingAdvancedSelectAll_Click;
        btnCancel.Click += BtnCancel_Click;

        ViewModel = new RoutingSettingViewModel(UpdateViewHandler);

        cmbdomainStrategy.ItemsSource = AppConfig.DomainStrategies;
        cmbdomainStrategy4Singbox.ItemsSource = AppConfig.DomainStrategies4Singbox;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.lstRoutings.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstRoutings.SelectedItem).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DomainStrategy, v => v.cmbdomainStrategy.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DomainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedRemoveCmd, v => v.menuRoutingAdvancedRemove).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedSetDefaultCmd, v => v.menuRoutingAdvancedSetDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules2).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                DialogResult = true;
                break;

            case EViewAction.ShowYesNo:
                if (UI.ShowYesNo(ResUI.RemoveRules) == MessageBoxResult.No)
                {
                    return false;
                }
                break;

            case EViewAction.RoutingRuleSettingWindow:

                if (obj is null)
                {
                    return false;
                }

                return new RoutingRuleSettingWindow((RoutingItem)obj).ShowDialog() ?? false;
        }
        return await Task.FromResult(true);
    }

    private void RoutingSettingWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            DialogResult = true;
        }
    }

    private void RoutingSettingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
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

    private void MenuRoutingAdvancedSelectAll_Click(object sender, RoutedEventArgs e)
    {
        lstRoutings.SelectAll();
    }

    private void LstRoutings_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ViewModel?.SelectedSources = lstRoutings.SelectedItems.Cast<RoutingItemModel>().ToList();
    }

    private void LstRoutings_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.RoutingAdvancedEditAsync(false);
    }

    private void LinkdomainStrategy_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://xtls.github.io/config/routing.html");
    }

    private void LinkdomainStrategy4Singbox_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/route/rule_action/#strategy");
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            DialogResult = true;
        }
        else
        {
            Close();
        }
    }
}
