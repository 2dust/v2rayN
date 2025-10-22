using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace v2rayN.Desktop.Views;

public partial class ClashConnectionsView : ReactiveUserControl<ClashConnectionsViewModel>
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
        return await Task.FromResult(true);
    }

    private void BtnAutofitColumnWidth_Click(object? sender, RoutedEventArgs e)
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

    private void btnClose_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClashConnectionClose(false);
    }
}
