using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ReactiveUI;

namespace v2rayN.Views;

/// <summary>
/// Interaction logic for ConnectionsView.xaml
/// </summary>
public partial class ClashConnectionsView
{
    public ClashConnectionsView()
    {
        InitializeComponent();
        ViewModel = new ClashConnectionsViewModel(UpdateViewHandler);
        btnAutofitColumnWidth.Click += BtnAutofitColumnWidth_Click;

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
                if (obj is null)
                    return false;
                Application.Current?.Dispatcher.Invoke((() =>
                {
                    ViewModel?.RefreshConnections((List<ConnectionItem>?)obj);
                }), DispatcherPriority.Normal);
                break;
        }

        return await Task.FromResult(true);
    }

    private void BtnAutofitColumnWidth_Click(object sender, RoutedEventArgs e)
    {
        AutofitColumnWidth();
    }

    private void AutofitColumnWidth()
    {
        try
        {
            foreach (var it in lstConnections.Columns)
            {
                it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ClashConnectionsView", ex);
        }
    }

    private void btnClose_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel?.ClashConnectionClose(false);
    }
}
