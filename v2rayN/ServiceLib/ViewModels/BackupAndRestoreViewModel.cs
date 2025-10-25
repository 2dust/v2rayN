namespace ServiceLib.ViewModels;

public partial class BackupAndRestoreViewModel : MyReactiveObject
{
    private readonly string _guiConfigs = "guiConfigs";
    private static string BackupFileName => $"backup_{DateTime.Now:yyyyMMddHHmmss}.zip";

    public ReactiveCommand<Unit, Unit> RemoteBackupCmd { get; }
    public ReactiveCommand<Unit, Unit> RemoteRestoreCmd { get; }
    public ReactiveCommand<Unit, Unit> WebDavCheckCmd { get; }

    [Reactive]
    private WebDavItem _selectedSource;

    [Reactive]
    private string _operationMsg;

    public BackupAndRestoreViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
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
        _ = await ConfigHandler.SaveConfig(_config);

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
        var fileName = Utils.GetBackupPath(BackupFileName);
        var result = await CreateZipFileFromDirectory(fileName);
        if (result)
        {
            var result2 = await WebDavManager.Instance.PutFile(fileName);
            if (result2)
            {
                DisplayOperationMsg(ResUI.OperationSuccess);
                return;
            }
        }

        DisplayOperationMsg(WebDavManager.Instance.GetLastError());
    }

    private async Task RemoteRestore()
    {
        DisplayOperationMsg();
        var fileName = Utils.GetTempPath(Utils.GetGuid());
        var result = await WebDavManager.Instance.GetRawFile(fileName);
        if (result)
        {
            await LocalRestore(fileName);
            return;
        }

        DisplayOperationMsg(WebDavManager.Instance.GetLastError());
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
            DisplayOperationMsg(WebDavManager.Instance.GetLastError());
        }

        return result;
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
        //check
        var lstFiles = FileManager.GetFilesFromZip(fileName);
        if (lstFiles is null || !lstFiles.Any(t => t.Contains(_guiConfigs)))
        {
            DisplayOperationMsg(ResUI.LocalRestoreInvalidZipTips);
            return;
        }

        //backup first
        var fileBackup = Utils.GetBackupPath(BackupFileName);
        var result = await CreateZipFileFromDirectory(fileBackup);
        if (result)
        {
            await AppManager.Instance.AppExitAsync(false);
            await SQLiteHelper.Instance.DisposeDbConnectionAsync();

            var toPath = Utils.GetConfigPath();
            FileManager.ZipExtractToFile(fileName, toPath, "");

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
        else
        {
            DisplayOperationMsg(WebDavManager.Instance.GetLastError());
        }
    }

    private async Task<bool> CreateZipFileFromDirectory(string fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return false;
        }

        var configDir = Utils.GetConfigPath();
        var configDirZipTemp = Utils.GetTempPath($"v2rayN_{DateTime.Now:yyyyMMddHHmmss}");
        var configDirTemp = Path.Combine(configDirZipTemp, _guiConfigs);

        FileManager.CopyDirectory(configDir, configDirTemp, false, true, "");
        var ret = FileManager.CreateFromDirectory(configDirZipTemp, fileName);
        Directory.Delete(configDirZipTemp, true);
        return await Task.FromResult(ret);
    }
}
