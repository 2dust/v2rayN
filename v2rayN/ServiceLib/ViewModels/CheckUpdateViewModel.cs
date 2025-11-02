namespace ServiceLib.ViewModels;

public class CheckUpdateViewModel : MyReactiveObject
{
    private const string _geo = "GeoFiles";
    private readonly string _v2rayN = ECoreType.v2rayN.ToString();
    private List<CheckUpdateModel> _lstUpdated = [];
    private static readonly string _tag = "CheckUpdateViewModel";

    public IObservableCollection<CheckUpdateModel> CheckUpdateModels { get; } = new ObservableCollectionExtended<CheckUpdateModel>();
    public ReactiveCommand<Unit, Unit> CheckUpdateCmd { get; }
    [Reactive] public bool EnableCheckPreReleaseUpdate { get; set; }

    public CheckUpdateViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        CheckUpdateCmd = ReactiveCommand.CreateFromTask(CheckUpdate);
        CheckUpdateCmd.ThrownExceptions.Subscribe(ex =>
        {
            Logging.SaveLog(_tag, ex);
            _ = UpdateView(_v2rayN, ex.Message);
        });

        EnableCheckPreReleaseUpdate = _config.CheckUpdateItem.CheckPreReleaseUpdate;

        this.WhenAnyValue(
        x => x.EnableCheckPreReleaseUpdate,
        y => y == true)
            .Subscribe(c => _config.CheckUpdateItem.CheckPreReleaseUpdate = EnableCheckPreReleaseUpdate);

        RefreshCheckUpdateItems();
    }

    private void RefreshCheckUpdateItems()
    {
        CheckUpdateModels.Clear();

        if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
        {
            CheckUpdateModels.Add(GetCheckUpdateModel(_v2rayN));
            //Not Windows and under Win10
            if (!(Utils.IsWindows() && Environment.OSVersion.Version.Major < 10))
            {
                CheckUpdateModels.Add(GetCheckUpdateModel(ECoreType.Xray.ToString()));
                CheckUpdateModels.Add(GetCheckUpdateModel(ECoreType.mihomo.ToString()));
                CheckUpdateModels.Add(GetCheckUpdateModel(ECoreType.sing_box.ToString()));
            }
        }
        CheckUpdateModels.Add(GetCheckUpdateModel(_geo));
    }

    private CheckUpdateModel GetCheckUpdateModel(string coreType)
    {
        if (coreType == _v2rayN && Utils.IsPackagedInstall())
        {
            return new()
            {
                IsSelected = false,
                CoreType = coreType,
                Remarks = ResUI.menuCheckUpdate + " (Not Support)",
            };
        }

        return new()
        {
            IsSelected = _config.CheckUpdateItem.SelectedCoreTypes?.Contains(coreType) ?? true,
            CoreType = coreType,
            Remarks = ResUI.menuCheckUpdate,
        };
    }

    private async Task SaveSelectedCoreTypes()
    {
        _config.CheckUpdateItem.SelectedCoreTypes = CheckUpdateModels.Where(t => t.IsSelected == true).Select(t => t.CoreType ?? "").ToList();
        await ConfigHandler.SaveConfig(_config);
    }

    private async Task CheckUpdate()
    {
        await Task.Run(CheckUpdateTask);
    }

    private async Task CheckUpdateTask()
    {
        _lstUpdated.Clear();
        _lstUpdated = CheckUpdateModels.Where(x => x.IsSelected == true)
                .Select(x => new CheckUpdateModel() { CoreType = x.CoreType }).ToList();
        await SaveSelectedCoreTypes();

        for (var k = CheckUpdateModels.Count - 1; k >= 0; k--)
        {
            var item = CheckUpdateModels[k];
            if (item.IsSelected != true)
            {
                continue;
            }

            await UpdateView(item.CoreType, "...");
            if (item.CoreType == _geo)
            {
                await CheckUpdateGeo();
            }
            else if (item.CoreType == _v2rayN)
            {
                if (Utils.IsPackagedInstall())
                {
                    await UpdateView(_v2rayN, "Not Support");
                    continue;
                }
                await CheckUpdateN(EnableCheckPreReleaseUpdate);
            }
            else if (item.CoreType == ECoreType.Xray.ToString())
            {
                await CheckUpdateCore(item, EnableCheckPreReleaseUpdate);
            }
            else
            {
                await CheckUpdateCore(item, false);
            }
        }

        await UpdateFinished();
    }

    private void UpdatedPlusPlus(string coreType, string fileName)
    {
        var item = _lstUpdated.FirstOrDefault(x => x.CoreType == coreType);
        if (item == null)
        {
            return;
        }
        item.IsFinished = true;
        if (!fileName.IsNullOrEmpty())
        {
            item.FileName = fileName;
        }
    }

    private async Task CheckUpdateGeo()
    {
        async Task _updateUI(bool success, string msg)
        {
            await UpdateView(_geo, msg);
            if (success)
            {
                UpdatedPlusPlus(_geo, "");
            }
        }
        await new UpdateService(_config, _updateUI).UpdateGeoFileAll()
            .ContinueWith(t => UpdatedPlusPlus(_geo, ""));
    }

    private async Task CheckUpdateN(bool preRelease)
    {
        async Task _updateUI(bool success, string msg)
        {
            await UpdateView(_v2rayN, msg);
            if (success)
            {
                await UpdateView(_v2rayN, ResUI.OperationSuccess);
                UpdatedPlusPlus(_v2rayN, msg);
            }
        }
        await new UpdateService(_config, _updateUI).CheckUpdateGuiN(preRelease)
            .ContinueWith(t => UpdatedPlusPlus(_v2rayN, ""));
    }

    private async Task CheckUpdateCore(CheckUpdateModel model, bool preRelease)
    {
        async Task _updateUI(bool success, string msg)
        {
            await UpdateView(model.CoreType, msg);
            if (success)
            {
                await UpdateView(model.CoreType, ResUI.MsgUpdateV2rayCoreSuccessfullyMore);

                UpdatedPlusPlus(model.CoreType, msg);
            }
        }
        var type = (ECoreType)Enum.Parse(typeof(ECoreType), model.CoreType);
        await new UpdateService(_config, _updateUI).CheckUpdateCore(type, preRelease)
            .ContinueWith(t => UpdatedPlusPlus(model.CoreType, ""));
    }

    private async Task UpdateFinished()
    {
        if (_lstUpdated.Count > 0 && _lstUpdated.Count(x => x.IsFinished == true) == _lstUpdated.Count)
        {
            await UpdateFinishedSub(false);
            await Task.Delay(2000);
            await UpgradeCore();

            if (_lstUpdated.Any(x => x.CoreType == _v2rayN && x.IsFinished == true))
            {
                await Task.Delay(1000);
                await UpgradeN();
            }
            await Task.Delay(1000);
            await UpdateFinishedSub(true);
        }
    }

    private async Task UpdateFinishedSub(bool blReload)
    {
        RxApp.MainThreadScheduler.Schedule(blReload, (scheduler, blReload) =>
        {
            _ = UpdateFinishedResult(blReload);
            return Disposable.Empty;
        });
        await Task.CompletedTask;
    }

    public async Task UpdateFinishedResult(bool blReload)
    {
        if (blReload)
        {
            AppEvents.ReloadRequested.Publish();
        }
        else
        {
            await CoreManager.Instance.CoreStop();
        }
    }

    private async Task UpgradeN()
    {
        try
        {
            var fileName = _lstUpdated.FirstOrDefault(x => x.CoreType == _v2rayN)?.FileName;
            if (fileName.IsNullOrEmpty())
            {
                return;
            }
            if (!Utils.UpgradeAppExists(out var upgradeFileName))
            {
                await UpdateView(_v2rayN, ResUI.UpgradeAppNotExistTip);
                NoticeManager.Instance.SendMessageAndEnqueue(ResUI.UpgradeAppNotExistTip);
                Logging.SaveLog("UpgradeApp does not exist");
                return;
            }

            var id = ProcUtils.ProcessStart(upgradeFileName, fileName, Utils.StartupPath());
            if (id > 0)
            {
                await AppManager.Instance.AppExitAsync(true);
            }
        }
        catch (Exception ex)
        {
            await UpdateView(_v2rayN, ex.Message);
        }
    }

    private async Task UpgradeCore()
    {
        foreach (var item in _lstUpdated)
        {
            if (item.FileName.IsNullOrEmpty())
            {
                continue;
            }

            var fileName = item.FileName;
            if (!File.Exists(fileName))
            {
                continue;
            }
            var toPath = Utils.GetBinPath("", item.CoreType);

            if (fileName.Contains(".tar.gz"))
            {
                FileManager.DecompressTarFile(fileName, toPath);
                var dir = new DirectoryInfo(toPath);
                if (dir.Exists)
                {
                    foreach (var subDir in dir.GetDirectories())
                    {
                        FileManager.CopyDirectory(subDir.FullName, toPath, false, true);
                        subDir.Delete(true);
                    }
                }
            }
            else if (fileName.Contains(".gz"))
            {
                FileManager.DecompressFile(fileName, toPath, item.CoreType);
            }
            else
            {
                FileManager.ZipExtractToFile(fileName, toPath, "geo");
            }

            if (Utils.IsNonWindows())
            {
                var filesList = new DirectoryInfo(toPath).GetFiles().Select(u => u.FullName).ToList();
                foreach (var file in filesList)
                {
                    await Utils.SetLinuxChmod(Path.Combine(toPath, item.CoreType.ToLower()));
                }
            }

            await UpdateView(item.CoreType, ResUI.MsgUpdateV2rayCoreSuccessfully);

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
    }

    private async Task UpdateView(string coreType, string msg)
    {
        var item = new CheckUpdateModel()
        {
            CoreType = coreType,
            Remarks = msg,
        };

        RxApp.MainThreadScheduler.Schedule(item, (scheduler, model) =>
        {
            _ = UpdateViewResult(model);
            return Disposable.Empty;
        });
        await Task.CompletedTask;
    }

    public async Task UpdateViewResult(CheckUpdateModel model)
    {
        var found = CheckUpdateModels.FirstOrDefault(t => t.CoreType == model.CoreType);
        if (found == null)
        {
            return;
        }
        found.Remarks = model.Remarks;
        await Task.CompletedTask;
    }
}
