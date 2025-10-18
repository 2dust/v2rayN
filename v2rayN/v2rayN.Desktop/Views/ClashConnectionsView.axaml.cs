using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;

namespace v2rayN.Desktop.Views;

public partial class ClashConnectionsView : ReactiveUserControl<ClashConnectionsViewModel>
{
    private const int MinColumnWidthPx = 30;

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

            // 监听可视区域尺寸变化，等比缩放列宽，保证铺满
            lstConnections
                .GetObservable(Visual.BoundsProperty)
                .Throttle(TimeSpan.FromMilliseconds(80))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ScaleColumnsToFit())
                .DisposeWith(disposables);
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
            // Auto 量测后按可用宽度等比缩放，保证铺满
            Dispatcher.UIThread.Post(ScaleColumnsToFit, DispatcherPriority.Background);
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

    private void ScaleColumnsToFit()
    {
        try
        {
            var visibleColumns = lstConnections.Columns.Where(c => c.IsVisible != false).ToList();
            if (visibleColumns.Count == 0)
            {
                return;
            }

            double viewportWidth = lstConnections.Bounds.Width;
            if (viewportWidth <= 0)
            {
                return;
            }

            const double scrollbarReserve = 18;
            double available = Math.Max(0, viewportWidth - scrollbarReserve);

            double desired = 0;
            foreach (var col in visibleColumns)
            {
                // 使用实际测量宽度作为期望，避免放大最小值导致不必要的扩张
                desired += Math.Max(1, col.ActualWidth);
            }

            if (desired <= 0 || available <= 0)
            {
                return;
            }

            double ratio = available / desired; // 可放大可缩小

            double remaining = available;
            int remainingCols = visibleColumns.Count;
            for (int i = 0; i < visibleColumns.Count; i++)
            {
                var col = visibleColumns[i];
                double proposed = Math.Floor(Math.Max(1, col.ActualWidth) * ratio);

                int colsLeft = remainingCols - 1;
                double maxThis = colsLeft > 0 ? Math.Max(0, remaining - colsLeft * MinColumnWidthPx) : remaining;
                double target = Math.Min(proposed, maxThis);

                if (i == visibleColumns.Count - 1)
                {
                    // 最后一列严格使用剩余空间，避免强制最小值导致溢出
                    target = Math.Max(0, remaining);
                }

                if (target < 0)
                {
                    target = 0;
                }

                col.Width = new DataGridLength(target, DataGridLengthUnitType.Pixel);
                remaining -= target;
                remainingCols--;
                if (remaining <= 0)
                {
                    remaining = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ClashConnectionsView", ex);
        }
    }
}
