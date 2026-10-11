namespace v2rayN.Views;

public partial class AddSshServerWindow
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
            txtPassword.Password = ViewModel.Password;
            txtPassphrase.Password = ViewModel.Passphrase;
            txtPassword.PasswordChanged += PasswordChanged;
            txtPassphrase.PasswordChanged += PassphraseChanged;
            new ActionDisposable(() =>
            {
                txtPassword.PasswordChanged -= PasswordChanged;
                txtPassphrase.PasswordChanged -= PassphraseChanged;
            }).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PrivateKeyAuth, v => v.chkPrivateKey.IsChecked).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Status, v => v.txtStatus.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.TestStatus, v => v.txtTestStatus.Text).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.IsTesting).Subscribe(testing =>
            {
                btnTest.Visibility = testing ? Visibility.Collapsed : Visibility.Visible;
                btnStopTest.Visibility = testing ? Visibility.Visible : Visibility.Collapsed;
            }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.PrivateKeyAuth).Subscribe(useKey =>
            {
                keyPanel.Visibility = useKey ? Visibility.Visible : Visibility.Collapsed;
                passwordPanel.Visibility = useKey ? Visibility.Collapsed : Visibility.Visible;
            }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.IsBusy).Subscribe(busy =>
            {
                form.IsEnabled = !busy;
                btnSave.IsEnabled = !busy;
                btnTest.IsEnabled = !busy;
            }).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.TestCmd, v => v.btnTest).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StopTestCmd, v => v.btnStopTest).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FetchCmd, v => v.btnFetch).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.TrustCmd, v => v.btnTrust).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BrowseCmd, v => v.btnBrowse).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SetupKeyCmd, v => v.btnSetupKey).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.Password).Subscribe(value => { if (txtPassword.Password != value) txtPassword.Password = value; }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.Passphrase).Subscribe(value => { if (txtPassphrase.Password != value) txtPassphrase.Password = value; }).DisposeWith(disposables);
            ViewModel.SetupKeyInteraction.RegisterHandler(interaction =>
            {
                new SshKeySetupWindow(interaction.Input) { Owner = this }.ShowDialog();
                interaction.SetOutput(interaction.Input.Result);
            }).DisposeWith(disposables);
            ViewModel.BrowseKeyInteraction.RegisterHandler(interaction =>
            {
                interaction.SetOutput(UI.OpenFileDialog(out var path, "All|*.*") == true ? path : null);
            }).DisposeWith(disposables);
            ViewModel.ConfirmHostKeyInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(UI.ShowYesNo(interaction.Input) == MessageBoxResult.Yes)).DisposeWith(disposables);
        });
    }
    private void PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Password = txtPassword.Password;
    private void PassphraseChanged(object sender, RoutedEventArgs e) => ViewModel.Passphrase = txtPassphrase.Password;
}
