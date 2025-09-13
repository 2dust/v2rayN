using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ReactiveUI;
using ServiceLib.Manager;
using v2rayN.Base;

namespace v2rayN.Views;

public partial class ProfilesSelectWindow
{
    private static Config _config;

    public Task<ProfileItem?> ProfileItem => GetProfileItem();
    public Task<List<ProfileItem>?> ProfileItems => GetProfileItems();
    private bool _allowMultiSelect = false;

    public ProfilesSelectWindow()
    {
        InitializeComponent();
        lstGroup.MaxHeight = Math.Floor(SystemParameters.WorkArea.Height * 0.20 / 40) * 40;

        _config = AppManager.Instance.Config;

        btnAutofitColumnWidth.Click += BtnAutofitColumnWidth_Click;
        txtServerFilter.PreviewKeyDown += TxtServerFilter_PreviewKeyDown;
        lstProfiles.PreviewKeyDown += LstProfiles_PreviewKeyDown;
        lstProfiles.SelectionChanged += LstProfiles_SelectionChanged;
        lstProfiles.LoadingRow += LstProfiles_LoadingRow;

        ViewModel = new ProfilesSelectViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.ProfileItems, v => v.lstProfiles.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedProfile, v => v.lstProfiles.SelectedItem).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstGroup.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSub, v => v.lstGroup.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ServerFilter, v => v.txtServerFilter.Text).DisposeWith(disposables);
        });

        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
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

    #region Event

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                this.DialogResult = true;
                break;
        }
        return await Task.FromResult(true);
    }

    private void LstProfiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedProfiles = lstProfiles.SelectedItems.Cast<ProfileItemModel>().ToList();
        }
    }

    private void LstProfiles_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Header = $" {e.Row.GetIndex() + 1}";
    }

    private void LstProfiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.SelectFinish();
    }

    private void LstProfiles_ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        var colHeader = sender as DataGridColumnHeader;
        if (colHeader == null || colHeader.TabIndex < 0 || colHeader.Column == null)
        {
            return;
        }

        var colName = ((MyDGTextColumn)colHeader.Column).ExName;
        ViewModel?.SortServer(colName);
    }

    private void menuSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (!_allowMultiSelect)
        {
            return;
        }
        lstProfiles.SelectAll();
    }

    private void LstProfiles_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            switch (e.Key)
            {
                case Key.A:
                    menuSelectAll_Click(null, null);
                    e.Handled = true;
                    break;
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

    private void BtnAutofitColumnWidth_Click(object sender, RoutedEventArgs e)
    {
        AutofitColumnWidth();
    }

    private void AutofitColumnWidth()
    {
        try
        {
            foreach (var it in lstProfiles.Columns)
            {
                it.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ProfilesView", ex);
        }
    }

    private void TxtServerFilter_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            ViewModel?.RefreshServers();
            e.Handled = true;
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

    #endregion Event
}
