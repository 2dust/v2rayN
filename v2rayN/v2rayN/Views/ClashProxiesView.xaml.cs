using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ReactiveUI;
using Splat;

namespace v2rayN.Views
{
    /// <summary>
    /// Interaction logic for ProxiesView.xaml
    /// </summary>
    public partial class ClashProxiesView
    {
        public ClashProxiesView()
        {
            InitializeComponent();
            ViewModel = new ClashProxiesViewModel(UpdateViewHandler);
            Locator.CurrentMutable.RegisterLazySingleton(() => ViewModel, typeof(ClashProxiesViewModel));
            lstProxyDetails.PreviewMouseDoubleClick += lstProxyDetails_PreviewMouseDoubleClick;

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.ProxyGroups, v => v.lstProxyGroups.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedGroup, v => v.lstProxyGroups.SelectedItem).DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.ProxyDetails, v => v.lstProxyDetails.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedDetail, v => v.lstProxyDetails.SelectedItem).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.ProxiesReloadCmd, v => v.menuProxiesReload).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ProxiesDelaytestCmd, v => v.menuProxiesDelaytest).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.ProxiesDelaytestPartCmd, v => v.menuProxiesDelaytestPart).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ProxiesSelectActivityCmd, v => v.menuProxiesSelectActivity).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.RuleModeSelected, v => v.cmbRulemode.SelectedIndex).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SortingSelected, v => v.cmbSorting.SelectedIndex).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
            });
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.DispatcherRefreshProxyGroups:
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.RefreshProxyGroups();
                    }), DispatcherPriority.Normal);
                    break;

                case EViewAction.DispatcherProxiesDelayTest:

                    if (obj is null)
                        return false;
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.ProxiesDelayTestResult((SpeedTestResult)obj);
                    }), DispatcherPriority.Normal);
                    break;
            }

            return await Task.FromResult(true);
        }

        private void ProxiesView_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F5:
                    ViewModel?.ProxiesReload();
                    break;

                case Key.Enter:
                    ViewModel?.SetActiveProxy();
                    break;
            }
        }

        private void lstProxyDetails_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.SetActiveProxy();
        }
    }
}
