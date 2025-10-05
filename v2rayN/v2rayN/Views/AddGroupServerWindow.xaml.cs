using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;

namespace v2rayN.Views;

public partial class AddGroupServerWindow
{
    public AddGroupServerWindow(ProfileItem profileItem)
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;
        this.Loaded += Window_Loaded;
        this.PreviewKeyDown += AddGroupServerWindow_PreviewKeyDown;
        lstChild.SelectionChanged += LstChild_SelectionChanged;
        menuSelectAllChild.Click += MenuSelectAllChild_Click;

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
                gridPolicyGroup.Visibility = Visibility.Collapsed;
                break;
        }

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CoreType, v => v.cmbCoreType.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PolicyGroupType, v => v.cmbPolicyGroupType.Text).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.ChildItemsObs, v => v.lstChild.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedChild, v => v.lstChild.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RemoveCmd, v => v.menuRemoveChildServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }

    private void AddGroupServerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!lstChild.IsKeyboardFocusWithin)
            return;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            if (e.Key == Key.A)
            {
                lstChild.SelectAll();
            }
        }
        else
        {
            if (e.Key == Key.T)
            {
                ViewModel?.MoveServer(EMove.Top);
            }
            else if (e.Key == Key.U)
            {
                ViewModel?.MoveServer(EMove.Up);
            }
            else if (e.Key == Key.D)
            {
                ViewModel?.MoveServer(EMove.Down);
            }
            else if (e.Key == Key.B)
            {
                ViewModel?.MoveServer(EMove.Bottom);
            }
            else if (e.Key == Key.Delete)
            {
                ViewModel?.ChildRemoveAsync();
            }
        }
    }

    private async void MenuAddChild_Click(object sender, RoutedEventArgs e)
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
        if (selectWindow.ShowDialog() == true)
        {
            var profiles = await selectWindow.ProfileItems;
            ViewModel?.ChildItemsObs.AddRange(profiles);
        }
    }

    private void LstChild_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedChildren = lstChild.SelectedItems.Cast<ProfileItem>().ToList();
        }
    }

    private void MenuSelectAllChild_Click(object sender, RoutedEventArgs e)
    {
        lstChild.SelectAll();
    }
}
