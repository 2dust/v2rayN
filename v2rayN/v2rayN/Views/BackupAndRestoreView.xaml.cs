namespace v2rayN.Views;

public partial class BackupAndRestoreView
{
    public BackupAndRestoreView()
    {
        InitializeComponent();
        menuLocalBackup.Click += MenuLocalBackup_Click;
        menuLocalRestore.Click += MenuLocalRestore_Click;

        ViewModel = new BackupAndRestoreViewModel(UpdateViewHandler);

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.OperationMsg, v => v.txtMsg.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SelectedSource.Url, v => v.txtWebDavUrl.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.UserName, v => v.txtWebDavUserName.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.Password, v => v.txtWebDavPassword.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource.DirName, v => v.txtWebDavDirName.Text).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.WebDavCheckCmd, v => v.menuWebDavCheck).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.RemoteBackupCmd, v => v.menuRemoteBackup).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RemoteRestoreCmd, v => v.menuRemoteRestore).DisposeWith(disposables);
        });
    }

    private void MenuLocalBackup_Click(object sender, RoutedEventArgs e)
    {
        if (UI.SaveFileDialog(out string fileName, "Zip|*.zip") != true)
        {
            return;
        }
        ViewModel?.LocalBackup(fileName);
    }

    private void MenuLocalRestore_Click(object sender, RoutedEventArgs e)
    {
        if (UI.OpenFileDialog(out string fileName, "Zip|*.zip|All|*.*") != true)
        {
            return;
        }
        ViewModel?.LocalRestore(fileName);
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        return await Task.FromResult(true);
    }
}
