
namespace v2rayN.Views;

public partial class SshKeySetupWindow
{
    private bool _allowClose;

    public SshKeySetupWindow(SshKeySetupViewModel model)
    {
        InitializeComponent();
        ViewModel = model;
        DataContext = model;
        btnCancel.Click += (_, _) => model.Cancel();
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            model.Cancel();
        };
        model.RequestClose += OnRequestClose;
        Closed += (_, _) => model.RequestClose -= OnRequestClose;
        this.WhenActivated(disposables =>
        {
            txtTarget.Text = model.TargetDescription;
            txtDiscovery.Text = model.DiscoveryMessage;
            listKeys.ItemsSource = model.Candidates;
            this.Bind(ViewModel, vm => vm.SelectedKey, v => v.listKeys.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.GenerateNew, v => v.radioGenerate.IsChecked).DisposeWith(disposables);
            radioReuse.Checked += ReuseSelected;
            new ActionDisposable(() => radioReuse.Checked -= ReuseSelected).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.GenerateNew).Subscribe(generate =>
            {
                radioReuse.IsChecked = !generate;
                confirmationPanel.Visibility = generate ? Visibility.Visible : Visibility.Collapsed;
                listKeys.IsEnabled = !generate;
            }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.NeedsPassword).Subscribe(needed =>
                passwordPanel.Visibility = needed ? Visibility.Visible : Visibility.Collapsed).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.IsBusy).Subscribe(busy =>
            {
                form.IsEnabled = !busy;
                btnRun.IsEnabled = !busy;
            }).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Status, v => v.txtStatus.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.PublicKeyText, v => v.txtPublicKey.Text).DisposeWith(disposables);
            txtPassphrase.PasswordChanged += PassphraseChanged;
            txtConfirmation.PasswordChanged += ConfirmationChanged;
            txtPassword.PasswordChanged += PasswordChanged;
            new ActionDisposable(() =>
            {
                txtPassphrase.PasswordChanged -= PassphraseChanged;
                txtConfirmation.PasswordChanged -= ConfirmationChanged;
                txtPassword.PasswordChanged -= PasswordChanged;
            }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.KeyPassphrase).Subscribe(value => { if (txtPassphrase.Password != value) txtPassphrase.Password = value; }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.ConfirmPassphrase).Subscribe(value => { if (txtConfirmation.Password != value) txtConfirmation.Password = value; }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.InstallationPassword).Subscribe(value => { if (txtPassword.Password != value) txtPassword.Password = value; }).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RunCmd, v => v.btnRun).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BrowseCmd, v => v.btnBrowse).DisposeWith(disposables);
            model.BrowseInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(UI.OpenFileDialog(out var path, "All|*.*") == true ? path : null)).DisposeWith(disposables);
        });
    }

    private void ReuseSelected(object? sender, RoutedEventArgs e)
    {
        if (radioReuse.IsChecked == true) ViewModel!.GenerateNew = false;
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        _allowClose = true;
        Close();
    }
    private void PassphraseChanged(object sender, RoutedEventArgs e) => ViewModel!.KeyPassphrase = txtPassphrase.Password;
    private void ConfirmationChanged(object sender, RoutedEventArgs e) => ViewModel!.ConfirmPassphrase = txtConfirmation.Password;
    private void PasswordChanged(object sender, RoutedEventArgs e) => ViewModel!.InstallationPassword = txtPassword.Password;

}
