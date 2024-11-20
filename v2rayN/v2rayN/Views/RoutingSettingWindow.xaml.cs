﻿using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;

namespace v2rayN.Views
{
    public partial class RoutingSettingWindow
    {
        public RoutingSettingWindow()
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            this.Closing += RoutingSettingWindow_Closing;
            this.PreviewKeyDown += RoutingSettingWindow_PreviewKeyDown;
            lstRoutings.SelectionChanged += lstRoutings_SelectionChanged;
            lstRoutings.MouseDoubleClick += LstRoutings_MouseDoubleClick;
            menuRoutingAdvancedSelectAll.Click += menuRoutingAdvancedSelectAll_Click;
            btnCancel.Click += btnCancel_Click;

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

                this.Bind(ViewModel, vm => vm.domainStrategy, v => v.cmbdomainStrategy.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainMatcher, v => v.cmbdomainMatcher.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainStrategy4Singbox, v => v.cmbdomainStrategy4Singbox.Text).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedRemoveCmd, v => v.menuRoutingAdvancedRemove).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedSetDefaultCmd, v => v.menuRoutingAdvancedSetDefault).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules2).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            });
            WindowsUtils.SetDarkBorder(this, AppHandler.Instance.Config.UiItem.FollowSystemTheme ? !WindowsUtils.IsLightTheme() : AppHandler.Instance.Config.UiItem.ColorModeDark);
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.CloseWindow:
                    this.DialogResult = true;
                    break;

                case EViewAction.ShowYesNo:
                    if (UI.ShowYesNo(ResUI.RemoveRules) == MessageBoxResult.No)
                    {
                        return false;
                    }
                    break;

                case EViewAction.RoutingRuleSettingWindow:

                    if (obj is null) return false;
                    return (new RoutingRuleSettingWindow((RoutingItem)obj)).ShowDialog() ?? false;
            }
            return await Task.FromResult(true);
        }

        private void RoutingSettingWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ViewModel?.IsModified == true)
            {
                this.DialogResult = true;
            }
        }

        private void RoutingSettingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
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

        private void menuRoutingAdvancedSelectAll_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            lstRoutings.SelectAll();
        }

        private void lstRoutings_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ViewModel.SelectedSources = lstRoutings.SelectedItems.Cast<RoutingItemModel>().ToList();
        }

        private void LstRoutings_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.RoutingAdvancedEditAsync(false);
        }

        private void linkdomainStrategy_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Utils.ProcessStart("https://xtls.github.io/config/routing.html");
        }

        private void linkdomainStrategy4Singbox_Click(object sender, RoutedEventArgs e)
        {
            Utils.ProcessStart("https://sing-box.sagernet.org/zh/configuration/shared/listen/#domain_strategy");
        }

        private void btnCancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ViewModel?.IsModified == true)
            {
                this.DialogResult = true;
            }
            else
            {
                this.Close();
            }
        }
    }
}