using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Runtime.InteropServices;

namespace ServiceLib.ViewModels
{
    public class CheckUpdateViewModel : MyReactiveObject
    {
        private const string _geo = "GeoFiles";
        private string _v2rayN = ECoreType.v2rayN.ToString();
        private List<CheckUpdateModel> _lstUpdated = [];

        private IObservableCollection<CheckUpdateModel> _checkUpdateModel = new ObservableCollectionExtended<CheckUpdateModel>();
        public IObservableCollection<CheckUpdateModel> CheckUpdateModels => _checkUpdateModel;
        public ReactiveCommand<Unit, Unit> CheckUpdateCmd { get; }
        [Reactive] public bool EnableCheckPreReleaseUpdate { get; set; }

        public CheckUpdateViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;

            CheckUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await CheckUpdate();
            });

            EnableCheckPreReleaseUpdate = _config.CheckUpdateItem.CheckPreReleaseUpdate;

            this.WhenAnyValue(
            x => x.EnableCheckPreReleaseUpdate,
            y => y == true)
                .Subscribe(c => { _config.CheckUpdateItem.CheckPreReleaseUpdate = EnableCheckPreReleaseUpdate; });

            RefreshCheckUpdateItems();
        }

        private void RefreshCheckUpdateItems()
        {
            _checkUpdateModel.Clear();

            if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
            {
                _checkUpdateModel.Add(GetCheckUpdateModel(_v2rayN));
                _checkUpdateModel.Add(GetCheckUpdateModel(ECoreType.Xray.ToString()));
                _checkUpdateModel.Add(GetCheckUpdateModel(ECoreType.mihomo.ToString()));
                _checkUpdateModel.Add(GetCheckUpdateModel(ECoreType.sing_box.ToString()));
            }
            _checkUpdateModel.Add(GetCheckUpdateModel(_geo));
        }

        private CheckUpdateModel GetCheckUpdateModel(string coreType)
        {
            return new()
            {
                IsSelected = _config.CheckUpdateItem.SelectedCoreTypes?.Contains(coreType) ?? true,
                CoreType = coreType,
                Remarks = ResUI.menuCheckUpdate,
            };
        }

        private async Task SaveSelectedCoreTypes()
        {
            _config.CheckUpdateItem.SelectedCoreTypes = _checkUpdateModel.Where(t => t.IsSelected == true).Select(t => t.CoreType ?? "").ToList();
            await ConfigHandler.SaveConfig(_config);
        }

        private async Task CheckUpdate()
        {
            _lstUpdated.Clear();
            _lstUpdated = _checkUpdateModel.Where(x => x.IsSelected == true)
                    .Select(x => new CheckUpdateModel() { CoreType = x.CoreType }).ToList();
            await SaveSelectedCoreTypes();

            for (var k = _checkUpdateModel.Count - 1; k >= 0; k--)
            {
                var item = _checkUpdateModel[k];
                if (item.IsSelected != true) continue;

                UpdateView(item.CoreType, "...");
                if (item.CoreType == _geo)
                {
                    await CheckUpdateGeo();
                }
                else if (item.CoreType == _v2rayN)
                {
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
            void _updateUI(bool success, string msg)
            {
                UpdateView(_geo, msg);
                if (success)
                {
                    UpdatedPlusPlus(_geo, "");
                }
            }
            await (new UpdateService()).UpdateGeoFileAll(_config, _updateUI)
                .ContinueWith(t =>
                {
                    UpdatedPlusPlus(_geo, "");
                });
        }

        private async Task CheckUpdateN(bool preRelease)
        {
            void _updateUI(bool success, string msg)
            {
                UpdateView(_v2rayN, msg);
                if (success)
                {
                    UpdateView(_v2rayN, ResUI.OperationSuccess);
                    UpdatedPlusPlus(_v2rayN, msg);
                }
            }
            await (new UpdateService()).CheckUpdateGuiN(_config, _updateUI, preRelease)
                .ContinueWith(t =>
                {
                    UpdatedPlusPlus(_v2rayN, "");
                });
        }

        private async Task CheckUpdateCore(CheckUpdateModel model, bool preRelease)
        {
            void _updateUI(bool success, string msg)
            {
                UpdateView(model.CoreType, msg);
                if (success)
                {
                    UpdateView(model.CoreType, ResUI.MsgUpdateV2rayCoreSuccessfullyMore);

                    UpdatedPlusPlus(model.CoreType, msg);
                }
            }
            var type = (ECoreType)Enum.Parse(typeof(ECoreType), model.CoreType);
            await (new UpdateService()).CheckUpdateCore(type, _config, _updateUI, preRelease)
                .ContinueWith(t =>
                {
                    UpdatedPlusPlus(model.CoreType, "");
                });
        }

        private async Task UpdateFinished()
        {
            if (_lstUpdated.Count > 0 && _lstUpdated.Count(x => x.IsFinished == true) == _lstUpdated.Count)
            {
                _updateView?.Invoke(EViewAction.DispatcherCheckUpdateFinished, false);
                await Task.Delay(2000);
                await UpgradeCore();

                if (_lstUpdated.Any(x => x.CoreType == _v2rayN && x.IsFinished == true))
                {
                    await Task.Delay(1000);
                    UpgradeN();
                }
                await Task.Delay(1000);
                _updateView?.Invoke(EViewAction.DispatcherCheckUpdateFinished, true);
            }
        }

        public void UpdateFinishedResult(bool blReload)
        {
            if (blReload)
            {
                Locator.Current.GetService<MainWindowViewModel>()?.Reload();
            }
            else
            {
                Locator.Current.GetService<MainWindowViewModel>()?.CloseCore();
            }
        }

        private void UpgradeN()
        {
            try
            {
                var fileName = _lstUpdated.FirstOrDefault(x => x.CoreType == _v2rayN)?.FileName;
                if (fileName.IsNullOrEmpty())
                {
                    return;
                }
                if (!Utils.UpgradeAppExists(out _))
                {
                    UpdateView(_v2rayN, ResUI.UpgradeAppNotExistTip);
                    return;
                }
                Locator.Current.GetService<MainWindowViewModel>()?.UpgradeApp(fileName);
            }
            catch (Exception ex)
            {
                UpdateView(_v2rayN, ex.Message);
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
                            FileManager.CopyDirectory(subDir.FullName, toPath, false, null);
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
                    FileManager.ZipExtractToFile(fileName, toPath, _config.GuiItem.IgnoreGeoUpdateCore ? "geo" : "");
                }

                if (Utils.IsNonWindows())
                {
                    var filesList = (new DirectoryInfo(toPath)).GetFiles().Select(u => u.FullName).ToList();
                    foreach (var file in filesList)
                    {
                        await Utils.SetLinuxChmod(Path.Combine(toPath, item.CoreType.ToLower()));
                    }
                }

                UpdateView(item.CoreType, ResUI.MsgUpdateV2rayCoreSuccessfully);

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        private void UpdateView(string coreType, string msg)
        {
            var item = new CheckUpdateModel()
            {
                CoreType = coreType,
                Remarks = msg,
            };
            _updateView?.Invoke(EViewAction.DispatcherCheckUpdate, item);
        }

        public void UpdateViewResult(CheckUpdateModel model)
        {
            var found = _checkUpdateModel.FirstOrDefault(t => t.CoreType == model.CoreType);
            if (found == null) return;
            var itemCopy = JsonUtils.DeepCopy(found);
            itemCopy.Remarks = model.Remarks;
            _checkUpdateModel.Replace(found, itemCopy);
        }
    }
}