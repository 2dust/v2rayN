using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using ReactiveUI;
using ServiceLib.Manager;

namespace v2rayN.Views;

public partial class SubSettingWindow
{
    public SubSettingWindow()
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;

        ViewModel = new SubSettingViewModel(UpdateViewHandler);
        this.Closing += SubSettingWindow_Closing;
        lstSubscription.MouseDoubleClick += LstSubscription_MouseDoubleClick;
        lstSubscription.SelectionChanged += LstSubscription_SelectionChanged;
        menuClose.Click += menuClose_Click;

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.SubItems, v => v.lstSubscription.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstSubscription.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SubAddCmd, v => v.menuSubAdd).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubDeleteCmd, v => v.menuSubDelete).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubEditCmd, v => v.menuSubEdit).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubShareCmd, v => v.menuSubShare).DisposeWith(disposables);
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

            case EViewAction.ShowYesNo:
                if (UI.ShowYesNo(ResUI.RemoveServer) == MessageBoxResult.No)
                {
                    return false;
                }
                break;

            case EViewAction.SubEditWindow:
                if (obj is null)
                    return false;
                return (new SubEditWindow((SubItem)obj)).ShowDialog() ?? false;

            case EViewAction.ShareSub:
                if (obj is null)
                    return false;
                ShareSub((string)obj);
                break;
        }
        return await Task.FromResult(true);
    }

    private async void ShareSub(string url)
    {
        if (url.IsNullOrEmpty())
        {
            return;
        }
        var img = QRCodeUtils.GetQRCode(url);
        var dialog = new QrcodeView()
        {
            imgQrcode = { Source = img },
            txtContent = { Text = url },
        };

        await DialogHost.Show(dialog, "SubDialog");
    }

    private void SubSettingWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            this.DialogResult = true;
        }
    }

    private void LstSubscription_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.EditSubAsync(false);
    }

    private void LstSubscription_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedSources = lstSubscription.SelectedItems.Cast<SubItem>().ToList();
        }
    }

    private void menuClose_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ViewModel?.IsModified == true)
        {
            this.DialogResult = true;
        }
        else
        {
            this.Close();
        }
    }
}
