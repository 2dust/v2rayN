using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using ReactiveUI;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class ProfilesSelectWindow : WindowBase<ProfilesSelectViewModel>
{
    private static Config _config;
    private const int MinColumnWidthPx = 30;

    public Task<ProfileItem?> ProfileItem => GetProfileItem();
    public Task<List<ProfileItem>?> ProfileItems => GetProfileItems();
    private bool _allowMultiSelect = false;

    public ProfilesSelectWindow()
    {
        InitializeComponent();

        _config = AppManager.Instance.Config;

        btnAutofitColumnWidth.Click += BtnAutofitColumnWidth_Click;
        txtServerFilter.KeyDown += TxtServerFilter_KeyDown;
        lstProfiles.KeyDown += LstProfiles_KeyDown;
        lstProfiles.SelectionChanged += LstProfiles_SelectionChanged;
        lstProfiles.LoadingRow += LstProfiles_LoadingRow;
        lstProfiles.Sorting += LstProfiles_Sorting;
        lstProfiles.DoubleTapped += LstProfiles_DoubleTapped;

        ViewModel = new ProfilesSelectViewModel(UpdateViewHandler);
        DataContext = ViewModel;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.ProfileItems, v => v.lstProfiles.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedProfile, v => v.lstProfiles.SelectedItem).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSub, v => v.lstGroup.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ServerFilter, v => v.txtServerFilter.Text).DisposeWith(disposables);

            // 监听可视区域变化，始终铺满
            lstProfiles
                .GetObservable(Visual.BoundsProperty)
                .Throttle(TimeSpan.FromMilliseconds(80))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ScaleColumnsToFit())
                .DisposeWith(disposables);
        });

        btnCancel.Click += (s, e) => Close(false);
    }

    public void AllowMultiSelect(bool allow)
    {
        _allowMultiSelect = allow;
        if (allow)
        {
            lstProfiles.SelectionMode = DataGridSelectionMode.Extended;
            lstProfiles.SelectedItems.Clear();
        }
        else
        {
            lstProfiles.SelectionMode = DataGridSelectionMode.Single;
            if (lstProfiles.SelectedItems.Count > 0)
            {
                var first = lstProfiles.SelectedItems[0];
                lstProfiles.SelectedItems.Clear();
                lstProfiles.SelectedItem = first;
            }
        }
    }

    // Expose ConfigType filter controls to callers
    public void SetConfigTypeFilter(IEnumerable<EConfigType> types, bool exclude = false)
        => ViewModel?.SetConfigTypeFilter(types, exclude);

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                Close(true);
                break;
        }
        return await Task.FromResult(true);
    }

    private void LstProfiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedProfiles = lstProfiles.SelectedItems.Cast<ProfileItemModel>().ToList();
        }
    }

    private void LstProfiles_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Header = $" {e.Row.Index + 1}";
    }

    private void LstProfiles_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // 忽略表头区域的双击
        if (e.Source is Control src)
        {
            if (src.FindAncestorOfType<DataGridColumnHeader>() != null)
            {
                e.Handled = true;
                return;
            }

            // 仅当在数据行或其子元素上双击时才触发选择
            if (src.FindAncestorOfType<DataGridRow>() != null)
            {
                ViewModel?.SelectFinish();
                e.Handled = true;
            }
        }
    }

    private void LstProfiles_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        // 自定义排序，防止默认行为导致误触发
        e.Handled = true;
        if (ViewModel != null && e.Column?.Tag?.ToString() != null)
        {
            ViewModel.SortServer(e.Column.Tag.ToString());
        }
    }

    private void LstProfiles_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            if (e.Key == Key.A)
            {
                if (_allowMultiSelect)
                {
                    lstProfiles.SelectAll();
                }
                e.Handled = true;
            }
        }
        else
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                ViewModel?.SelectFinish();
                e.Handled = true;
            }
        }
    }

    private void BtnAutofitColumnWidth_Click(object? sender, RoutedEventArgs e)
    {
        AutofitColumnWidth();
    }

    private void AutofitColumnWidth()
    {
        try
        {
            foreach (var col in lstProfiles.Columns)
            {
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
            // Auto 量测后按可视宽度等比缩放，保证铺满
            Dispatcher.UIThread.Post(ScaleColumnsToFit, DispatcherPriority.Background);
        }
        catch
        {
        }
    }

    private void TxtServerFilter_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            ViewModel?.RefreshServers();
        }
    }

    public async Task<ProfileItem?> GetProfileItem()
    {
        var item = await ViewModel?.GetProfileItem();
        return item;
    }

    public async Task<List<ProfileItem>?> GetProfileItems()
    {
        var item = await ViewModel?.GetProfileItems();
        return item;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Trigger selection finalize when Confirm is clicked
        ViewModel?.SelectFinish();
    }

    private void ScaleColumnsToFit()
    {
        try
        {
            var visibleColumns = lstProfiles.Columns.Where(c => c.IsVisible != false).ToList();
            if (visibleColumns.Count == 0)
            {
                return;
            }

            double viewportWidth = lstProfiles.Bounds.Width;
            if (viewportWidth <= 0)
            {
                return;
            }

            const double scrollbarReserve = 18;
            double available = Math.Max(0, viewportWidth - scrollbarReserve);

            double desired = 0;
            foreach (var col in visibleColumns)
            {
                desired += Math.Max(1, col.ActualWidth);
            }

            if (desired <= 0 || available <= 0)
            {
                return;
            }

            double ratio = available / desired; // 可放大也可缩小

            double remaining = available;
            int remainingCols = visibleColumns.Count;
            for (int i = 0; i < visibleColumns.Count; i++)
            {
                var col = visibleColumns[i];
                double proposed = Math.Floor(Math.Max(1, col.ActualWidth) * ratio);

                // 为后续列预留最小宽度（若总宽不足，也可能为 0）
                int colsLeft = remainingCols - 1;
                double maxThis = colsLeft > 0 ? Math.Max(0, remaining - colsLeft * MinColumnWidthPx) : remaining;
                double target = Math.Min(proposed, maxThis);

                if (i == visibleColumns.Count - 1)
                {
                    // 最后一列严格使用剩余空间，避免溢出
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
        catch
        {
        }
    }
}
