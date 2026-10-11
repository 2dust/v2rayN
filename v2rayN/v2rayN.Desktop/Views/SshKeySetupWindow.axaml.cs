using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class SshKeySetupWindow : WindowBase<SshKeySetupViewModel>
{
    private bool _allowClose;

    public SshKeySetupWindow() => InitializeComponent();

    public SshKeySetupWindow(SshKeySetupViewModel model) : this()
    {
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
            radioReuse.IsCheckedChanged += ReuseSelected;
            new ActionDisposable(() => radioReuse.IsCheckedChanged -= ReuseSelected).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.GenerateNew).Subscribe(generate =>
            {
                radioReuse.IsChecked = !generate;
                confirmationPanel.IsVisible = generate;
                listKeys.IsEnabled = !generate;
            }).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.NeedsPassword).Subscribe(needed =>
                passwordPanel.IsVisible = needed).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.IsBusy).Subscribe(busy =>
            {
                form.IsEnabled = !busy;
                btnRun.IsEnabled = !busy;
            }).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Status, v => v.txtStatus.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.PublicKeyText, v => v.txtPublicKey.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.KeyPassphrase, v => v.txtPassphrase.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ConfirmPassphrase, v => v.txtConfirmation.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.InstallationPassword, v => v.txtPassword.Text).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RunCmd, v => v.btnRun).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BrowseCmd, v => v.btnBrowse).DisposeWith(disposables);
            model.BrowseInteraction.RegisterHandler(async interaction =>
                interaction.SetOutput(await UI.OpenFileDialog(null))).DisposeWith(disposables);
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

}
