using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using ReactiveUI;

namespace v2rayN.Views;

public partial class RoutingRuleSettingWindow
{
    public RoutingRuleSettingWindow(RoutingItem routingItem)
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;
        this.Loaded += Window_Loaded;
        this.PreviewKeyDown += RoutingRuleSettingWindow_PreviewKeyDown;
        lstRules.SelectionChanged += lstRules_SelectionChanged;
        lstRules.MouseDoubleClick += LstRules_MouseDoubleClick;
        menuRuleSelectAll.Click += menuRuleSelectAll_Click;
        btnBrowseCustomIcon.Click += btnBrowseCustomIcon_Click;
        btnBrowseCustomRulesetPath4Singbox.Click += btnBrowseCustomRulesetPath4Singbox_Click;

        ViewModel = new RoutingRuleSettingViewModel(routingItem, UpdateViewHandler);
        Global.DomainStrategies.ForEach(it =>
        {
            cmbdomainStrategy.Items.Add(it);
        });
        cmbdomainStrategy.Items.Add(string.Empty);
        Global.DomainStrategies4Singbox.ForEach(it =>
        {
            cmbdomainStrategy4Singbox.Items.Add(it);
        });

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

            case EViewAction.ShowYesNo:

                if (UI.ShowYesNo(ResUI.RemoveServer) == MessageBoxResult.No)
                {
                    return false;
                }
                break;

            case EViewAction.AddBatchRoutingRulesYesNo:

                if (UI.ShowYesNo(ResUI.AddBatchRoutingRulesYesNo) == MessageBoxResult.No)
                {
                    return false;
                }
                break;

            case EViewAction.RoutingRuleDetailsWindow:

                if (obj is null)
                    return false;
                return (new RoutingRuleDetailsWindow((RulesItem)obj)).ShowDialog() ?? false;

            case EViewAction.ImportRulesFromFile:

                if (UI.OpenFileDialog(out string fileName, "Rules|*.json|All|*.*") != true)
                {
                    return false;
                }
                ViewModel?.ImportRulesFromFileAsync(fileName);
                break;

            case EViewAction.SetClipboardData:
                if (obj is null)
                    return false;
                WindowsUtils.SetClipboardData((string)obj);
                break;

            case EViewAction.ImportRulesFromClipboard:
                var clipboardData = WindowsUtils.GetClipboardData();
                if (clipboardData.IsNotEmpty())
                {
                    ViewModel?.ImportRulesFromClipboardAsync(clipboardData);
                }
                break;
        }

        return await Task.FromResult(true);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void RoutingRuleSettingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
            if (e.Key == Key.T)
            {
                ViewModel?.MoveRule(EMove.Top);
            }
            else if (e.Key == Key.U)
            {
                ViewModel?.MoveRule(EMove.Up);
            }
            else if (e.Key == Key.D)
            {
                ViewModel?.MoveRule(EMove.Down);
            }
            else if (e.Key == Key.B)
            {
                ViewModel?.MoveRule(EMove.Bottom);
            }
            else if (e.Key == Key.Delete)
            {
                ViewModel?.RuleRemoveAsync();
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
        if (UI.OpenFileDialog(out string fileName,
            "PNG,ICO|*.png;*.ico") != true)
        {
            return;
        }

        txtCustomIcon.Text = fileName;
    }

    private void btnBrowseCustomRulesetPath4Singbox_Click(object sender, RoutedEventArgs e)
    {
        if (UI.OpenFileDialog(out string fileName,
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
