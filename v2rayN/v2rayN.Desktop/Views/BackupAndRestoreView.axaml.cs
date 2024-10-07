using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Reactive.Disposables;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views
{
    public partial class BackupAndRestoreView : ReactiveUserControl<BackupAndRestoreViewModel>
    {
        private Window _window;

        public BackupAndRestoreView(Window window)
        {
            _window = window;

            InitializeComponent();
            menuLocalBackup.Click += MenuLocalBackup_Click;
            menuLocalRestore.Click += MenuLocalRestore_Click;

            ViewModel = new BackupAndRestoreViewModel(UpdateViewHandler);

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

        private async void MenuLocalBackup_Click(object? sender, RoutedEventArgs e)
        {
            var fileName = await UI.SaveFileDialog(_window, "Zip|*.zip");
            if (fileName.IsNullOrEmpty())
            {
                return;
            }

            ViewModel?.LocalBackup(fileName);
        }

        private async void MenuLocalRestore_Click(object? sender, RoutedEventArgs e)
        {
            var fileName = await UI.OpenFileDialog(_window, null);
            if (fileName.IsNullOrEmpty())
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