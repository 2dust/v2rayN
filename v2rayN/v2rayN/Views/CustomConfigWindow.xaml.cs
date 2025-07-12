using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;

namespace v2rayN.Views;

public partial class CustomConfigWindow
{
    private static Config _config;

    public CustomConfigWindow()
    {
        InitializeComponent();

        this.Owner = Application.Current.MainWindow;
        _config = AppHandler.Instance.Config;

        ViewModel = new CustomConfigViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.EnableCustomConfig4Ray, v => v.rayCustomConfigEnable.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CustomConfig4Ray, v => v.rayCustomConfig.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.EnableCustomConfig4Singbox, v => v.sbCustomConfigEnable.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CustomConfig4Singbox, v => v.sbCustomConfig.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.CustomTunConfig4Singbox, v => v.sbCustomTunConfig.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
        WindowsUtils.SetDarkBorder(this, AppHandler.Instance.Config.UiItem.CurrentTheme);
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
}
