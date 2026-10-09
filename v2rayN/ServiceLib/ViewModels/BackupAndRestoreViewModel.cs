namespace ServiceLib.ViewModels;

public partial class BackupAndRestoreViewModel : MyReactiveObject
{
    private static string BackupFileName => $"backup_{DateTime.Now:yyyyMMddHHmmss}.zip";

    public ReactiveCommand<RxVoid, RxVoid> RemoteBackupCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RemoteRestoreCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> WebDavCheckCmd { get; }

    [Reactive]
    public partial WebDavItem SelectedSource { get; set; }

    [Reactive]
    public partial string OperationMsg { get; set; } = string.Empty;

    public BackupAndRestoreViewModel()
    {
        _config = AppManager.Instance.Config;

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
        if (!await SaveWebDavSettings())
        {
            return;
        }

        var result = await WebDavManager.Instance.CheckConnection();
        if (result)
        {
            DisplayOperationMsg(ResUI.OperationSuccess);
        }
        else
        {
            DisplayOperationMsg(WebDavManager.Instance.GetLastError());
        }
    }

    private async Task RemoteBackup()
    {
        DisplayOperationMsg();
        if (!await SaveWebDavSettings())
        {
            return;
        }

        var fileName = Utils.GetBackupPath(BackupFileName);
        if (!CreateZipFileFromDirectory(fileName, SelectedSource.ExcludeFromRemoteBackup))
        {
            DisplayOperationMsg(ResUI.OperationFailed);
            return;
        }

        var result = await WebDavManager.Instance.PutFile(fileName);
        if (result)
        {
            DisplayOperationMsg(ResUI.OperationSuccess);
            return;
        }

        DisplayOperationMsg(WebDavManager.Instance.GetLastError());
    }

    private async Task RemoteRestore()
    {
        DisplayOperationMsg();
        if (!await SaveWebDavSettings())
        {
            return;
        }

        var fileName = Utils.GetTempPath(Utils.GetGuid());
        var result = await WebDavManager.Instance.GetRawFile(fileName);
        if (result)
        {
            await LocalRestore(fileName);
            return;
        }

        DisplayOperationMsg(WebDavManager.Instance.GetLastError());
    }

    private async Task<bool> SaveWebDavSettings()
    {
        _config.WebDavItem = SelectedSource;
        if (await ConfigHandler.SaveConfig(_config) == 0)
        {
            return true;
        }

        DisplayOperationMsg(ResUI.OperationFailed);
        return false;
    }

    public Task<bool> LocalBackup(string fileName)
    {
        DisplayOperationMsg();
        var result = CreateZipFileFromDirectory(fileName);
        if (result)
        {
            DisplayOperationMsg(ResUI.OperationSuccess);
        }
        else
        {
            DisplayOperationMsg(ResUI.OperationFailed);
        }

        return Task.FromResult(result);
    }

    public async Task LocalRestore(string fileName)
    {
        DisplayOperationMsg();
        if (fileName.IsNullOrEmpty())
        {
            return;
        }
        //exist
        if (!File.Exists(fileName))
        {
            return;
        }
        if (!BackupArchiveService.TryStageRestore(fileName, _config.WebDavItem, out var stageDirectory))
        {
            DisplayOperationMsg(ResUI.LocalRestoreInvalidZipTips);
            return;
        }

        try
        {
            //backup first
            var fileBackup = Utils.GetBackupPath(BackupFileName);
            if (!CreateZipFileFromDirectory(fileBackup))
            {
                DisplayOperationMsg(ResUI.OperationFailed);
                return;
            }

            await AppManager.Instance.AppExitAsync(false);
            await SQLiteHelper.Instance.DisposeDbConnectionAsync();

            var toPath = Utils.GetConfigPath();
            FileUtils.CopyDirectory(stageDirectory, toPath, false, true);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(BackupAndRestoreViewModel), ex);
            DisplayOperationMsg(ResUI.OperationFailed);
            return;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stageDirectory))
                {
                    Directory.Delete(stageDirectory, true);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(BackupAndRestoreViewModel), ex);
            }
        }

        if (Utils.IsWindows())
        {
            ProcUtils.RebootAsAdmin(false);
        }
        else
        {
            if (Utils.UpgradeAppExists(out var upgradeFileName))
            {
                _ = ProcUtils.ProcessStart(upgradeFileName, Global.RebootAs, Utils.StartupPath());
            }
        }
        AppManager.Instance.Shutdown(true);
    }

    private static bool CreateZipFileFromDirectory(string fileName, bool excludeWebDavSettings = false)
    {
        if (fileName.IsNullOrEmpty())
        {
            return false;
        }

        return BackupArchiveService.TryCreateBackup(Utils.GetConfigPath(), fileName, excludeWebDavSettings);
    }
}
