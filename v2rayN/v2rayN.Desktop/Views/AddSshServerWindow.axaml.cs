using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class AddSshServerWindow : WindowBase<AddSshServerViewModel>
{
    public AddSshServerWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();
        Closed += (_, _) => ViewModel?.CancelOperation();
        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Address, v => v.txtAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Port, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Username, v => v.txtUsername.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PrivateKeyPath, v => v.txtPrivateKeyPath.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.HostKey, v => v.txtHostKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Password, v => v.txtPassword.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Passphrase, v => v.txtPassphrase.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PrivateKeyAuth, v => v.chkPrivateKey.IsChecked).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Status, v => v.txtStatus.Text).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.PrivateKeyAuth).Subscribe(useKey =>
            {
                keyPanel.IsVisible = useKey;
                passwordPanel.IsVisible = !useKey;
            }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.IsBusy).Subscribe(busy =>
            {
                form.IsEnabled = !busy;
                btnSave.IsEnabled = !busy;
            }).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FetchCmd, v => v.btnFetch).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.TrustCmd, v => v.btnTrust).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BrowseCmd, v => v.btnBrowse).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetupKeyCmd, v => v.btnSetupKey).DisposeWith(disposables);
            ViewModel.SetupKeyInteraction.RegisterHandler(async interaction =>
            {
                var window = new SshKeySetupWindow(interaction.Input);
                await window.ShowDialog(this);
                interaction.SetOutput(interaction.Input.Result);
            }).DisposeWith(disposables);
            ViewModel.BrowseKeyInteraction.RegisterHandler(async interaction =>
                interaction.SetOutput(await UI.OpenFileDialog(null))).DisposeWith(disposables);
            ViewModel.ConfirmHostKeyInteraction.RegisterHandler(async interaction =>
                interaction.SetOutput(await UI.ShowYesNo(interaction.Input) == ButtonResult.Yes)).DisposeWith(disposables);
        });
    }
}
