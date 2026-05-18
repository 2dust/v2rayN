namespace ServiceLib.ViewModels;

public class CheckUpdateViewModel : MyReactiveObject
{
    private const string _geo = "GeoFiles";
    private readonly ECoreType _v2rayN = ECoreType.v2rayN;
    private List<CheckUpdateModel> _lstUpdated = [];
    private static readonly string _tag = "CheckUpdateViewModel";

    public IObservableCollection<CheckUpdateModel> CheckUpdateModels { get; } = new ObservableCollectionExtended<CheckUpdateModel>();
    public ReactiveCommand<Unit, Unit> CheckUpdateCmd { get; }
    public ReactiveCommand<Unit, Unit> CheckOnlyCmd { get; }
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

        CheckOnlyCmd = ReactiveCommand.CreateFromTask(CheckOnly);
        CheckOnlyCmd.ThrownExceptions.Subscribe(ex =>
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

        foreach (var type in CoreInfoManager.Instance.GetCheckUpdateCoreTypes())
        {
            CheckUpdateModels.Add(GetCheckUpdateModel(type));
        }

        CheckUpdateModels.Add(GetGeoFileCheckUpdateModel());
    }

    private CheckUpdateModel GetCheckUpdateModel(ECoreType coreType)
    {
        if (coreType == _v2rayN && Utils.IsPackagedInstall())
        {
            return new()
            {
                IsSelected = false,
                CoreType = coreType,
                IsGeoFile = false,
                Remarks = ResUI.menuCheckUpdate + $" ({ResUI.MsgNotSupport})",
            };
        }

        AppManager.Instance.LastCheckUpdateResults.TryGetValue(coreType, out var lastResult);
        return new()
        {
            IsSelected = _config.CheckUpdateItem.SelectedCoreTypes?.Contains(coreType.ToString()) ?? true,
            CoreType = coreType,
            IsGeoFile = false,
            Remarks = lastResult ?? ResUI.menuCheckUpdate,
        };
    }

    private CheckUpdateModel GetGeoFileCheckUpdateModel()
    {
        return new()
        {
            IsSelected = _config.CheckUpdateItem.SelectedCoreTypes?.Contains(_geo) ?? true,
            CoreType = null,
            IsGeoFile = true,
            Remarks = ResUI.menuCheckUpdate,
        };
    }

    private async Task SaveSelectedCoreTypes()
    {
        _config.CheckUpdateItem.SelectedCoreTypes = CheckUpdateModels
            .Where(t => t.IsSelected == true)
            .Select(t => t.CoreTypeForStorage)
            .ToList();
        await ConfigHandler.SaveConfig(_config);
    }

    private async Task CheckOnly()
    {
        await Task.Run(CheckOnlyTask);
    }

    private async Task CheckUpdate()
    {
        await Task.Run(CheckUpdateTask);
    }

    private async Task CheckOnlyTask()
    {
        await SaveSelectedCoreTypes();

        for (var k = CheckUpdateModels.Count - 1; k >= 0; k--)
        {
            var item = CheckUpdateModels[k];
            if (item.IsSelected != true)
            {
                continue;
            }

            await UpdateView(item.CoreType, "...");

            if (item.IsGeoFile || item.CoreType == null)
            {
                await UpdateView(item.CoreType, ResUI.menuCheckOnly + $" ({ResUI.MsgNotSupport})");
                continue;
            }

            if (item.CoreType == null)
            {
                await UpdateView(item.CoreType, ResUI.MsgNotSupport);
                continue;
            }

            var updateService = new UpdateService(_config, async (success, msg) => await Task.CompletedTask);
            var result = await updateService.CheckHasUpdateOnly(item.CoreType.Value, EnableCheckPreReleaseUpdate);
            if (result.Success && result.Version != null)
            {
                await UpdateView(item.CoreType, string.Format(ResUI.MsgCheckUpdateHasNewVersion, item.CoreType, result.Version));
            }
            else
            {
                await UpdateView(item.CoreType, result.Msg);
            }
        }
    }

    private async Task CheckUpdateTask()
    {
        _lstUpdated.Clear();
        _lstUpdated = CheckUpdateModels
            .Where(x => x.IsSelected == true)
            .Select(x => new CheckUpdateModel()
            {
                CoreType = x.CoreType,
                IsGeoFile = x.IsGeoFile
            })
            .ToList();
        await SaveSelectedCoreTypes();

        for (var k = CheckUpdateModels.Count - 1; k >= 0; k--)
        {
            var item = CheckUpdateModels[k];
            if (item.IsSelected != true)
            {
                continue;
            }

            await UpdateView(item.CoreType, "...");

            if (item.IsGeoFile)
            {
                await CheckUpdateGeo();
            }
            else if (item.CoreType == _v2rayN)
            {
                if (Utils.IsPackagedInstall())
                {
                    await UpdateView(_v2rayN, ResUI.MsgNotSupport);
                    continue;
                }
                await CheckUpdateN(EnableCheckPreReleaseUpdate);
            }
            else if (item.CoreType == ECoreType.Xray)
            {
                await CheckUpdateCore(item, EnableCheckPreReleaseUpdate);
            }
            else if (item.CoreType.HasValue)
            {
                await CheckUpdateCore(item, false);
            }
        }

        await UpdateFinished();
    }

    private void UpdatedPlusPlus(ECoreType? coreType, string fileName)
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
            await UpdateView(null, msg);
            if (success)
            {
                UpdatedPlusPlus(null, "");
            }
        }
        await new UpdateService(_config, _updateUI).UpdateGeoFileAll()
            .ContinueWith(t => UpdatedPlusPlus(null, ""));
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

        if (model.CoreType.HasValue)
        {
            await new UpdateService(_config, _updateUI).CheckUpdateCore(model.CoreType.Value, preRelease)
                .ContinueWith(t => UpdatedPlusPlus(model.CoreType, ""));
        }
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
        RxSchedulers.MainThreadScheduler.Schedule(blReload, (scheduler, blReload) =>
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
            if (item.FileName.IsNullOrEmpty() || item.IsGeoFile)
            {
                continue;
            }

            var fileName = item.FileName;
            if (!File.Exists(fileName))
            {
                continue;
            }

            var coreTypeStr = item.CoreType?.ToString() ?? "";
            var toPath = Utils.GetBinPath("", coreTypeStr);

            if (fileName.Contains(".tar.gz"))
            {
                FileUtils.DecompressTarFile(fileName, toPath);
                var dir = new DirectoryInfo(toPath);
                if (dir.Exists)
                {
                    foreach (var subDir in dir.GetDirectories())
                    {
                        FileUtils.CopyDirectory(subDir.FullName, toPath, false, true);
                        subDir.Delete(true);
                    }
                }
            }
            else if (fileName.Contains(".gz"))
            {
                FileUtils.DecompressFile(fileName, toPath, coreTypeStr);
            }
            else
            {
                FileUtils.ZipExtractToFile(fileName, toPath, "geo");
            }

            if (Utils.IsNonWindows())
            {
                var filesList = new DirectoryInfo(toPath).GetFiles().Select(u => u.FullName).ToList();
                foreach (var file in filesList)
                {
                    await Utils.SetLinuxChmod(Path.Combine(toPath, coreTypeStr.ToLower()));
                }
            }

            await UpdateView(item.CoreType, ResUI.MsgUpdateV2rayCoreSuccessfully);

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
    }

    private async Task UpdateView(ECoreType? coreType, string msg)
    {
        var item = new CheckUpdateModel()
        {
            CoreType = coreType,
            IsGeoFile = coreType == null,
            Remarks = msg,
        };

        RxSchedulers.MainThreadScheduler.Schedule(item, (scheduler, model) =>
        {
            _ = UpdateViewResult(model);
            return Disposable.Empty;
        });
        await Task.CompletedTask;
    }

    public async Task UpdateViewResult(CheckUpdateModel model)
    {
        var found = CheckUpdateModels.FirstOrDefault(t => t.CoreType == model.CoreType && t.IsGeoFile == model.IsGeoFile);
        if (found == null)
        {
            return;
        }
        found.Remarks = model.Remarks;
        await Task.CompletedTask;
    }
}
