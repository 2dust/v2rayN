using System.Windows;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive.Disposables;

namespace v2rayN.Views;

public partial class AddGroupServerWindow
{
    public AddGroupServerWindow(ProfileItem profileItem)
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;
        this.Loaded += Window_Loaded;
        this.PreviewKeyDown += AddGroupServerWindow_PreviewKeyDown;

        ViewModel = new AddGroupServerViewModel(profileItem, UpdateViewHandler);

        cmbCoreType.ItemsSource = Global.CoreTypes;
        cmbPolicyGroupType.ItemsSource = new List<string>
        {
            ResUI.TbLeastPing,
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

            this.BindCommand(ViewModel, vm => vm.RemoveCmd, v => v.menuRemoveChildServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveTopCmd, v => v.menuMoveTop).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveUpCmd, v => v.menuMoveUp).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveDownCmd, v => v.menuMoveDown).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.MoveBottomCmd, v => v.menuMoveBottom).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
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
        if (selectWindow.ShowDialog() == true)
        {
            var profile = await selectWindow.ProfileItem;
            if (profile != null)
            {
                if (ViewModel != null)
                {
                    ViewModel.ChildItemsObs.Add(profile);
                }
            }
        }
    }
}
