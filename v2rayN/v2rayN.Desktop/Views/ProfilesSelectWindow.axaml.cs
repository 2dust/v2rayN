using Avalonia.VisualTree;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class ProfilesSelectWindow : WindowBase<ProfilesSelectViewModel>
{
    private static Config _config;

    public Task<ProfileItem?> ProfileItem => GetProfileItem();
    public Task<List<ProfileItem>?> ProfileItems => GetProfileItems();
    private bool _allowMultiSelect;

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
        ViewModel?.SelectedProfiles = lstProfiles.SelectedItems.Cast<ProfileItemModel>().ToList();
    }

    private void LstProfiles_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Header = $" {e.Row.Index + 1}";
    }

    private void LstProfiles_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Ignore double-taps on the column header area
        if (e.Source is Control src)
        {
            if (src.FindAncestorOfType<DataGridColumnHeader>() != null)
            {
                e.Handled = true;
                return;
            }

            // Only trigger selection when double-tapped on a data row or its child element
            if (src.FindAncestorOfType<DataGridRow>() != null)
            {
                ViewModel?.SelectFinish();
                e.Handled = true;
            }
        }
    }

    private void LstProfiles_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        // Custom sort to prevent unintended default behavior
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
}
