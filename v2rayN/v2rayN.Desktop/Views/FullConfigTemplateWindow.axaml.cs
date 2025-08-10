using System.Reactive.Disposables;
using Avalonia.Interactivity;
using ReactiveUI;
using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class FullConfigTemplateWindow : WindowBase<FullConfigTemplateViewModel>
{
    private static Config _config;

    public FullConfigTemplateWindow()
    {
        InitializeComponent();

        _config = AppHandler.Instance.Config;
        btnCancel.Click += (s, e) => this.Close();
        ViewModel = new FullConfigTemplateViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.EnableFullConfigTemplate4Ray, v => v.rayFullConfigTemplateEnable.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.FullConfigTemplate4Ray, v => v.rayFullConfigTemplate.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AddProxyOnly4Ray, v => v.togAddProxyProtocolOutboundOnly4Ray.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ProxyDetour4Ray, v => v.txtProxyDetour4Ray.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.EnableFullConfigTemplate4Singbox, v => v.sbFullConfigTemplateEnable.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.FullConfigTemplate4Singbox, v => v.sbFullConfigTemplate.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.FullTunConfigTemplate4Singbox, v => v.sbFullTunConfigTemplate.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.AddProxyOnly4Singbox, v => v.togAddProxyProtocolOutboundOnly4Singbox.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ProxyDetour4Singbox, v => v.txtProxyDetour4Singbox.Text).DisposeWith(disposables);

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
