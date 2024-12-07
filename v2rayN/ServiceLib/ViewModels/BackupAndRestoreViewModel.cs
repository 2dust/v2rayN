using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class BackupAndRestoreViewModel : MyReactiveObject
    {
        private readonly string _guiConfigs = "guiConfigs";
        private static string BackupFileName => $"backup_{DateTime.Now:yyyyMMddHHmmss}.zip";

        public ReactiveCommand<Unit, Unit> RemoteBackupCmd { get; }
        public ReactiveCommand<Unit, Unit> RemoteRestoreCmd { get; }
        public ReactiveCommand<Unit, Unit> WebDavCheckCmd { get; }

        [Reactive]
        public WebDavItem SelectedSource { get; set; }

        [Reactive]
        public string OperationMsg { get; set; }

        public BackupAndRestoreViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;

            WebDavCheckCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await WebDavCheck();
            });
            RemoteBackupCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RemoteBackup();
            });
            RemoteRestoreCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RemoteRestore();
            });

            SelectedSource = JsonUtils.DeepCopy(_config.WebDavItem);
        }

        private void DisplayOperationMsg(string msg = "")
        {
            OperationMsg = msg;
        }

        private async Task WebDavCheck()
        {
            DisplayOperationMsg();
            _config.WebDavItem = SelectedSource;
            await ConfigHandler.SaveConfig(_config);

            var result = await WebDavHandler.Instance.CheckConnection();
            if (result)
            {
                DisplayOperationMsg(ResUI.OperationSuccess);
            }
            else
            {
                DisplayOperationMsg(WebDavHandler.Instance.GetLastError());
            }
        }

        private async Task RemoteBackup()
        {
            DisplayOperationMsg();
            var fileName = Utils.GetBackupPath(BackupFileName);
            var result = await CreateZipFileFromDirectory(fileName);
            if (result)
            {
                var result2 = await WebDavHandler.Instance.PutFile(fileName);
                if (result2)
                {
                    DisplayOperationMsg(ResUI.OperationSuccess);
                    return;
                }
            }

            DisplayOperationMsg(WebDavHandler.Instance.GetLastError());
        }

        private async Task RemoteRestore()
        {
            DisplayOperationMsg();
            var fileName = Utils.GetTempPath(Utils.GetGuid());
            var result = await WebDavHandler.Instance.GetRawFile(fileName);
            if (result)
            {
                await LocalRestore(fileName);
                return;
            }

            DisplayOperationMsg(WebDavHandler.Instance.GetLastError());
        }

        public async Task<bool> LocalBackup(string fileName)
        {
            DisplayOperationMsg();
            var result = await CreateZipFileFromDirectory(fileName);
            if (result)
            {
                DisplayOperationMsg(ResUI.OperationSuccess);
            }
            else
            {
                DisplayOperationMsg(WebDavHandler.Instance.GetLastError());
            }

            return result;
        }

        public async Task LocalRestore(string fileName)
        {
            DisplayOperationMsg();
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            //exist
            if (!File.Exists(fileName))
            {
                return;
            }
            //check
            var lstFiles = FileManager.GetFilesFromZip(fileName);
            if (lstFiles is null || !lstFiles.Any(t => t.Contains(_guiConfigs)))
            {
                DisplayOperationMsg(ResUI.LocalRestoreInvalidZipTips);
                return;
            }
            if (!Utils.UpgradeAppExists(out _))
            {
                DisplayOperationMsg(ResUI.UpgradeAppNotExistTip);
                return;
            }

            //backup first
            var fileBackup = Utils.GetBackupPath(BackupFileName);
            var result = await CreateZipFileFromDirectory(fileBackup);
            if (result)
            {
                Locator.Current.GetService<MainWindowViewModel>()?.UpgradeApp(fileName);
            }
            else
            {
                DisplayOperationMsg(WebDavHandler.Instance.GetLastError());
            }
        }

        private async Task<bool> CreateZipFileFromDirectory(string fileName)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var configDir = Utils.GetConfigPath();
            var configDirZipTemp = Utils.GetTempPath($"v2rayN_{DateTime.Now:yyyyMMddHHmmss}");
            var configDirTemp = Path.Combine(configDirZipTemp, _guiConfigs);

            FileManager.CopyDirectory(configDir, configDirTemp, false, "cache.db");
            var ret = FileManager.CreateFromDirectory(configDirZipTemp, fileName);
            Directory.Delete(configDirZipTemp, true);
            return ret;
        }
    }
}