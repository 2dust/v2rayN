using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using ServiceLib.Manager;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class ProfilesSelectWindow : ReactiveWindow<ProfilesSelectViewModel>
{
    private static Config _config;

    public Task<ProfileItem?> ProfileItem => GetFirstProfileItemAsync();

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

        btnSave.Click += (s, e) => ViewModel?.SelectFinish();
        btnCancel.Click += (s, e) => Close(false);
    }

    public void AllowMultiSelect(bool allow)
    {
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
        ViewModel?.SelectFinish();
    }

    private async void LstProfiles_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        if (ViewModel != null && e.Column?.Tag?.ToString() != null)
        {
            await ViewModel.SortServer(e.Column.Tag.ToString());
        }
        e.Handled = false;
    }

    private void LstProfiles_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            if (e.Key == Key.A)
            {
                lstProfiles.SelectAll();
            }
        }
        else
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                ViewModel?.SelectFinish();
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

    public async Task<ProfileItem?> GetFirstProfileItemAsync()
    {
        var item = await ViewModel?.GetProfileItem();
        return item;
    }
}
