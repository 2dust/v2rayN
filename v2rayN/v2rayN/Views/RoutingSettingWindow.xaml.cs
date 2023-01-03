using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows.Input;
using v2rayN.Mode;
using v2rayN.ViewModels;

namespace v2rayN.Views
{
    public partial class RoutingSettingWindow
    {
        public RoutingSettingWindow()
        {
            InitializeComponent();
            this.Closing += RoutingSettingWindow_Closing;
            this.PreviewKeyDown += RoutingSettingWindow_PreviewKeyDown;
            lstRoutings.SelectionChanged += lstRoutings_SelectionChanged;
            lstRoutings.MouseDoubleClick += LstRoutings_MouseDoubleClick;

            ViewModel = new RoutingSettingViewModel(this);

            Global.domainStrategys.ForEach(it =>
            {
                cmbdomainStrategy.Items.Add(it);
            });
            Global.domainMatchers.ForEach(it =>
            {
                cmbdomainMatcher.Items.Add(it);
            });


            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.lstRoutings.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstRoutings.SelectedItem).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.enableRoutingAdvanced, v => v.togenableRoutingAdvanced.IsChecked).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainStrategy, v => v.cmbdomainStrategy.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.domainMatcher, v => v.cmbdomainMatcher.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.ProxyDomain, v => v.txtProxyDomain.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.ProxyIP, v => v.txtProxyIP.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.DirectDomain, v => v.txtDirectDomain.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.DirectIP, v => v.txtDirectIP.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.BlockDomain, v => v.txtBlockDomain.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.BlockIP, v => v.txtBlockIP.Text).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.enableRoutingBasic, v => v.menuRoutingBasic.IsEnabled).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.enableRoutingAdvanced, v => v.menuRoutingAdvanced.IsEnabled).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.enableRoutingBasic, v => v.tabBasic.Visibility).DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.enableRoutingAdvanced, v => v.tabAdvanced.Visibility).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.RoutingBasicImportRulesCmd, v => v.menuRoutingBasicImportRules).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedAddCmd, v => v.menuRoutingAdvancedAdd2).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedRemoveCmd, v => v.menuRoutingAdvancedRemove).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedSetDefaultCmd, v => v.menuRoutingAdvancedSetDefault).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RoutingAdvancedImportRulesCmd, v => v.menuRoutingAdvancedImportRules2).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);

            });
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
            ViewModel?.RoutingAdvancedEdit(false);
        }

        private void linkdomainStrategy_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Utils.ProcessStart("https://www.v2fly.org/config/routing.html");
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
