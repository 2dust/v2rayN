using v2rayN.Manager;


namespace v2rayN.Views;

public partial class AppRoutingWindow
{
    public AppRoutingWindow()
    {
        InitializeComponent();
        btnClose.Click += (_, _) => Close();
        txtPassword.PasswordChanged += (_, _) => { if (ViewModel != null) { ViewModel.SocksPassword = txtPassword.Password; } };
        this.WhenActivated(disposables =>
        {
            DataContext = ViewModel;
            this.OneWayBind(ViewModel, vm => vm.IsProfile, v => v.ProfilePanel.Visibility).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.IsInterface, v => v.InterfacePanel.Visibility).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.IsSocks, v => v.SocksPanel.Visibility).DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel.SocksPassword).Subscribe(value => { if (txtPassword.Password != value) { txtPassword.Password = value ?? ""; } }).DisposeWith(disposables);
            ViewModel.BrowseExecutable.RegisterHandler(interaction =>
            {
                interaction.SetOutput(UI.OpenFileDialog(out var path, "Executable|*.exe") == true ? path : null);
            }).DisposeWith(disposables);
            ViewModel.PickProcess.RegisterHandler(interaction =>
            {
                var picker = new AppRoutingAppPickerWindow { Owner = this, ViewModel = ViewModel, DataContext = ViewModel };
                interaction.SetOutput(picker.ShowDialog() == true ? ViewModel.SelectedProcess : null);
            }).DisposeWith(disposables);
        });
    }
}
