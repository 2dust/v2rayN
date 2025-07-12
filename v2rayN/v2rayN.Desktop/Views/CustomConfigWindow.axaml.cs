using System.Reactive.Disposables;
using Avalonia.Interactivity;
using ReactiveUI;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class CustomConfigWindow : WindowBase<CustomConfigViewModel>
{
    private static Config _config;

    public CustomConfigWindow()
    {
        InitializeComponent();

        _config = AppHandler.Instance.Config;
        btnCancel.Click += (s, e) => this.Close();
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
}
