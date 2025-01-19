using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using System.Reactive.Disposables;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class RoutingRuleSettingWindow : ReactiveWindow<RoutingRuleSettingViewModel>
    {
        public RoutingRuleSettingWindow()
        {
            InitializeComponent();
        }

        public RoutingRuleSettingWindow(RoutingItem routingItem)
        {
            InitializeComponent();

            this.Loaded += Window_Loaded;
            btnCancel.Click += (s, e) => this.Close();
            this.KeyDown += RoutingRuleSettingWindow_KeyDown;
            lstRules.SelectionChanged += lstRules_SelectionChanged;
            lstRules.DoubleTapped += LstRules_DoubleTapped;
            menuRuleSelectAll.Click += menuRuleSelectAll_Click;
            //btnBrowseCustomIcon.Click += btnBrowseCustomIcon_Click;
            btnBrowseCustomRulesetPath4Singbox.Click += btnBrowseCustomRulesetPath4Singbox_ClickAsync;

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
                this.Bind(ViewModel, vm => vm.SelectedRouting.DomainStrategy, v => v.cmbdomainStrategy.SelectedValue).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedRouting.DomainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.SelectedValue).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SelectedRouting.Url, v => v.txtUrl.Text).DisposeWith(disposables);
                //this.Bind(ViewModel, vm => vm.SelectedRouting.CustomIcon, v => v.txtCustomIcon.Text).DisposeWith(disposables);
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
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.CloseWindow:
                    this.Close(true);
                    break;

                case EViewAction.ShowYesNo:
                    if (await UI.ShowYesNo(this, ResUI.RemoveServer) == ButtonResult.No)
                    {
                        return false;
                    }
                    break;

                case EViewAction.AddBatchRoutingRulesYesNo:
                    if (await UI.ShowYesNo(this, ResUI.AddBatchRoutingRulesYesNo) == ButtonResult.No)
                    {
                        return false;
                    }
                    break;

                case EViewAction.RoutingRuleDetailsWindow:
                    if (obj is null) return false;
                    return await new RoutingRuleDetailsWindow((RulesItem)obj).ShowDialog<bool>(this);

                case EViewAction.ImportRulesFromFile:
                    var fileName = await UI.OpenFileDialog(this, null);
                    if (fileName.IsNullOrEmpty())
                    {
                        return false;
                    }
                    ViewModel?.ImportRulesFromFileAsync(fileName);
                    break;

                case EViewAction.SetClipboardData:
                    if (obj is null) return false;
                    await AvaUtils.SetClipboardData(this, (string)obj);
                    break;

                case EViewAction.ImportRulesFromClipboard:
                    var clipboardData = await AvaUtils.GetClipboardData(this);
                    ViewModel?.ImportRulesFromClipboardAsync(clipboardData);
                    break;
            }

            return await Task.FromResult(true);
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            txtRemarks.Focus();
        }

        private void RoutingRuleSettingWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
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

        private void lstRules_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedSources = lstRules.SelectedItems.Cast<RulesItemModel>().ToList();
        }

        private void LstRules_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            ViewModel?.RuleEditAsync(false);
        }

        private void menuRuleSelectAll_Click(object? sender, RoutedEventArgs e)
        {
            lstRules.SelectAll();
        }

        //private async void btnBrowseCustomIcon_Click(object? sender, RoutedEventArgs e)
        //{
        //    var fileName = await UI.OpenFileDialog(this, FilePickerFileTypes.ImagePng);
        //    if (fileName.IsNullOrEmpty())
        //    {
        //        return;
        //    }

        //    txtCustomIcon.Text = fileName;
        //}

        private async void btnBrowseCustomRulesetPath4Singbox_ClickAsync(object? sender, RoutedEventArgs e)
        {
            var fileName = await UI.OpenFileDialog(this, null);
            if (fileName.IsNullOrEmpty())
            {
                return;
            }

            txtCustomRulesetPath4Singbox.Text = fileName;
        }

        private void linkCustomRulesetPath4Singbox(object? sender, RoutedEventArgs e)
        {
            ProcUtils.ProcessStart("https://github.com/2dust/v2rayCustomRoutingList/blob/master/singbox_custom_ruleset_example.json");
        }
    }
}