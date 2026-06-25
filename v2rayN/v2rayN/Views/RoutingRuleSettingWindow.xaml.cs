namespace v2rayN.Views;

public partial class RoutingRuleSettingWindow
{
    public RoutingRuleSettingWindow(RoutingItem routingItem)
    {
        InitializeComponent();

        Owner = Application.Current.MainWindow;
        Loaded += Window_Loaded;
        PreviewKeyDown += RoutingRuleSettingWindow_PreviewKeyDown;
        lstRules.SelectionChanged += lstRules_SelectionChanged;
        lstRules.MouseDoubleClick += LstRules_MouseDoubleClick;
        menuRuleSelectAll.Click += menuRuleSelectAll_Click;
        btnBrowseCustomIcon.Click += btnBrowseCustomIcon_Click;
        btnBrowseCustomRulesetPath4Singbox.Click += btnBrowseCustomRulesetPath4Singbox_Click;

        ViewModel = new RoutingRuleSettingViewModel(routingItem);

        cmbdomainStrategy.ItemsSource = Global.DomainStrategies.AppendEmpty();
        cmbdomainStrategy4Singbox.ItemsSource = Global.DomainStrategies4Sbox;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.RulesItems, v => v.lstRules.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstRules.SelectedItem).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedRouting.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting.DomainStrategy, v => v.cmbdomainStrategy.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting.DomainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedRouting.Url, v => v.txtUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting.CustomIcon, v => v.txtCustomIcon.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting.CustomRulesetPath4Singbox, v => v.txtCustomRulesetPath4Singbox.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting.Sort, v => v.txtSort.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RuleAddCmd, v => v.menuRuleAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ImportRulesFromFileCmd, v => v.menuImportRulesFromFile).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ImportRulesFromClipboardCmd, v => v.menuImportRulesFromClipboard).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ImportRulesFromUrlCmd, v => v.menuImportRulesFromUrl).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RuleAddCmd, v => v.menuRuleAdd2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RuleRemoveCmd, v => v.menuRuleRemove).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RuleExportSelectedCmd, v => v.menuRuleExportSelected).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);

            ViewModel.CloseWindowInteraction.RegisterHandler(interaction =>
            {
                DialogResult = true;
                interaction.SetOutput(Unit.Default);
            }).DisposeWith(disposables);

            ViewModel.ShowYesNoInteraction.RegisterHandler(interaction =>
            {
                var message = interaction.Input;
                var result = UI.ShowYesNo(message) != MessageBoxResult.No;
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel.SetClipboardDataInteraction.RegisterHandler(interaction =>
            {
                var strData = interaction.Input;
                WindowsUtils.SetClipboardData(strData);
                interaction.SetOutput(Unit.Default);
            }).DisposeWith(disposables);

            ViewModel.ReadTextFromClipboardInteraction.RegisterHandler(interaction =>
            {
                var clipboardData = WindowsUtils.GetClipboardData();
                interaction.SetOutput(clipboardData);
            }).DisposeWith(disposables);

            ViewModel.BrowseRulesFileInteraction.RegisterHandler(interaction =>
            {
                if (UI.OpenFileDialog(out var fileName, "Rules|*.json|All|*.*") != true)
                {
                    interaction.SetOutput(null);
                    return;
                }
                interaction.SetOutput(fileName);
            }).DisposeWith(disposables);

            ViewModel.ShowRoutingRuleDetailsInteraction.RegisterHandler(interaction =>
            {
                var rulesItem = interaction.Input;
                if (rulesItem is null)
                {
                    interaction.SetOutput(false);
                    return;
                }
                var result = new RoutingRuleDetailsWindow(rulesItem).ShowDialog() ?? false;
                interaction.SetOutput(result);
            }).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void RoutingRuleSettingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!lstRules.IsKeyboardFocusWithin)
        {
            return;
        }

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            if (e.Key == Key.A)
            {
                lstRules.SelectAll();
            }
            else if (e.Key == Key.C)
            {
                ViewModel?.RuleExportSelectedAsync();
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.T:
                    ViewModel?.MoveRule(EMove.Top);
                    break;

                case Key.U:
                    ViewModel?.MoveRule(EMove.Up);
                    break;

                case Key.D:
                    ViewModel?.MoveRule(EMove.Down);
                    break;

                case Key.B:
                    ViewModel?.MoveRule(EMove.Bottom);
                    break;

                case Key.Delete:
                case Key.Back:
                    ViewModel?.RuleRemoveAsync();
                    break;
            }
        }
    }

    private void lstRules_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedSources = lstRules.SelectedItems.Cast<RulesItemModel>().ToList();
        }
    }

    private void LstRules_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.RuleEditAsync(false);
    }

    private void menuRuleSelectAll_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        lstRules.SelectAll();
    }

    private void btnBrowseCustomIcon_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (UI.OpenFileDialog(out var fileName,
            "PNG,ICO|*.png;*.ico") != true)
        {
            return;
        }

        txtCustomIcon.Text = fileName;
    }

    private void btnBrowseCustomRulesetPath4Singbox_Click(object sender, RoutedEventArgs e)
    {
        if (UI.OpenFileDialog(out var fileName,
              "Config|*.json|All|*.*") != true)
        {
            return;
        }

        txtCustomRulesetPath4Singbox.Text = fileName;
    }

    private void linkCustomRulesetPath4Singbox(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://github.com/2dust/v2rayCustomRoutingList/blob/master/singbox_custom_ruleset_example.json");
    }
}
