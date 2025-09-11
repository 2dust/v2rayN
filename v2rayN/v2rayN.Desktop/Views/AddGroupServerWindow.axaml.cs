using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DynamicData;
using ReactiveUI;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class AddGroupServerWindow : WindowBase<AddGroupServerViewModel>
{
    public AddGroupServerWindow()
    {
        InitializeComponent();
    }

    public AddGroupServerWindow(ProfileItem profileItem)
    {
        InitializeComponent();

        this.Loaded += Window_Loaded;
        btnCancel.Click += (s, e) => this.Close();
        lstChild.SelectionChanged += LstChild_SelectionChanged;

        ViewModel = new AddGroupServerViewModel(profileItem, UpdateViewHandler);

        cmbCoreType.ItemsSource = Global.CoreTypes;
        cmbPolicyGroupType.ItemsSource = new List<string>
        {
            ResUI.TbLeastPing,
            ResUI.TbFallback,
            ResUI.TbRandom,
            ResUI.TbRoundRobin,
            ResUI.TbLeastLoad,
        };

        switch (profileItem.ConfigType)
        {
            case EConfigType.PolicyGroup:
                this.Title = ResUI.TbConfigTypePolicyGroup;
                break;
            case EConfigType.ProxyChain:
                this.Title = ResUI.TbConfigTypeProxyChain;
                gridPolicyGroup.IsVisible = false;
                break;
        }

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PolicyGroupType, v => v.cmbPolicyGroupType.SelectedValue).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.ChildItemsObs, v => v.lstChild.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedChild, v => v.lstChild.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RemoveCmd, v => v.menuRemoveChildServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });

        // Context menu actions that require custom logic (Add, SelectAll)
        menuAddChildServer.Click += MenuAddChild_Click;
        menuSelectAllChild.Click += (s, e) => lstChild.SelectAll();

        // Keyboard shortcuts when focus is within grid
        this.AddHandler(KeyDownEvent, AddGroupServerWindow_KeyDown, RoutingStrategies.Tunnel);
        lstChild.LoadingRow += LstChild_LoadingRow;
    }

    private void LstChild_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Header = $" {e.Row.Index + 1}";
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                this.Close(true);
                break;
        }
        return await Task.FromResult(true);
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void AddGroupServerWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!lstChild.IsKeyboardFocusWithin)
            return;

        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            if (e.Key == Key.A)
            {
                lstChild.SelectAll();
                e.Handled = true;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.T:
                    ViewModel?.MoveServer(EMove.Top);
                    e.Handled = true;
                    break;
                case Key.U:
                    ViewModel?.MoveServer(EMove.Up);
                    e.Handled = true;
                    break;
                case Key.D:
                    ViewModel?.MoveServer(EMove.Down);
                    e.Handled = true;
                    break;
                case Key.B:
                    ViewModel?.MoveServer(EMove.Bottom);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    ViewModel?.ChildRemoveAsync();
                    e.Handled = true;
                    break;
            }
        }
    }

    private async void MenuAddChild_Click(object? sender, RoutedEventArgs e)
    {
        var selectWindow = new ProfilesSelectWindow();
        if (ViewModel?.SelectedSource?.ConfigType == EConfigType.PolicyGroup)
        {
            selectWindow.SetConfigTypeFilter(new[] { EConfigType.Custom }, exclude: true);
        }
        else
        {
            selectWindow.SetConfigTypeFilter(new[] { EConfigType.Custom, EConfigType.PolicyGroup, EConfigType.ProxyChain }, exclude: true);
        }
        selectWindow.AllowMultiSelect(true);
        var result = await selectWindow.ShowDialog<bool?>(this);
        if (result == true)
        {
            var profiles = await selectWindow.ProfileItems;
            ViewModel?.ChildItemsObs.AddRange(profiles);
        }
    }

    private void LstChild_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedChildren = lstChild.SelectedItems.Cast<ProfileItem>().ToList();
        }
    }

}
