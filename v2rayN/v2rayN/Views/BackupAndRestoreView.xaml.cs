using ReactiveUI;
using Splat;
using System.Reactive.Disposables;
using System.Windows;

namespace v2rayN.Views
{
    public partial class BackupAndRestoreView
    {
        private NoticeHandler? _noticeHandler;

        public BackupAndRestoreView()
        {
            InitializeComponent();
            menuLocalBackup.Click += MenuLocalBackup_Click;
            menuLocalRestore.Click += MenuLocalRestore_Click;

            ViewModel = new BackupAndRestoreViewModel(UpdateViewHandler);

            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            this.WhenActivated(disposables =>
            {
                this.Bind(ViewModel, vm => vm.OperationMsg, v => v.txtMsg.Text).DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SelectedSource.url, v => v.txtWebDavUrl.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.userName, v => v.txtWebDavUserName.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.password, v => v.txtWebDavPassword.Text).DisposeWith(disposables);
                this.Bind(ViewModel, vm => vm.SelectedSource.dirName, v => v.txtWebDavDirName.Text).DisposeWith(disposables);
                
                this.BindCommand(ViewModel, vm => vm.WebDavCheckCmd, v => v.menuWebDavCheck).DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.RemoteBackupCmd, v => v.menuRemoteBackup).DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.RemoteRestoreCmd, v => v.menuRemoteRestore).DisposeWith(disposables);
            });
        }

        private void MenuRemoteRestore_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
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
}