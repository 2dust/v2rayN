namespace v2rayN.Views;

public partial class FullConfigTemplateWindow
{
    private static Config _config;

    public FullConfigTemplateWindow()
    {
        InitializeComponent();

        Owner = Application.Current.MainWindow;
        _config = AppManager.Instance.Config;

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
        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.CloseWindow:
                DialogResult = true;
                break;
        }
        return await Task.FromResult(true);
    }

    private void linkFullConfigTemplateDoc_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart("https://github.com/2dust/v2rayN/wiki/Description-of-some-ui#%E5%AE%8C%E6%95%B4%E9%85%8D%E7%BD%AE%E6%A8%A1%E6%9D%BF%E8%AE%BE%E7%BD%AE");
    }
}
