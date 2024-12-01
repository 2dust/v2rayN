using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Threading;

namespace v2rayN.Views
{
    /// <summary>
    /// Interaction logic for ConnectionsView.xaml
    /// </summary>
    public partial class ClashConnectionsView
    {
        public ClashConnectionsView()
        {
            InitializeComponent();
            ViewModel = new ClashConnectionsViewModel(UpdateViewHandler);

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.ConnectionItems, v => v.lstConnections.ItemsSource).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstConnections.SelectedItem).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.ConnectionCloseCmd, v => v.menuConnectionClose).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ConnectionCloseAllCmd, v => v.menuConnectionCloseAll).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.HostFilter, v => v.txtHostFilter.Text).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.ConnectionCloseAllCmd, v => v.btnConnectionCloseAll).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.AutoRefresh, v => v.togAutoRefresh.IsChecked).DisposeWith(disposables);
            });
        }

        private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
        {
            switch (action)
            {
                case EViewAction.DispatcherRefreshConnections:
                    if (obj is null) return false;
                    Application.Current?.Dispatcher.Invoke((() =>
                    {
                        ViewModel?.RefreshConnections((List<ConnectionItem>?)obj);
                    }), DispatcherPriority.Normal);
                    break;
            }

            return await Task.FromResult(true);
        }

        private void btnClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel?.ClashConnectionClose(false);
        }
    }
}