namespace v2rayN.Views;

public partial class NetBridgeView
{
    public NetBridgeView()
    {
        InitializeComponent();

        ViewModel = new NetBridgeViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveRulesCmd, v => v.btnSave).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.EnableNetBridge, v => v.togEnableNetBridge.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.RuleProcess, v => v.txtRuleProcess.Text).DisposeWith(disposables);

        });
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        return await Task.FromResult(true);
    }
}
