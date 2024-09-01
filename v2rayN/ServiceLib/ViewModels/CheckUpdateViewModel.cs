using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Diagnostics;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class CheckUpdateViewModel : MyReactiveObject
    {
        private const string _geo = "GeoFiles";
        private List<CheckUpdateItem> _lstUpdated = [];

        private IObservableCollection<CheckUpdateItem> _checkUpdateItem = new ObservableCollectionExtended<CheckUpdateItem>();
        public IObservableCollection<CheckUpdateItem> CheckUpdateItems => _checkUpdateItem;
        public ReactiveCommand<Unit, Unit> CheckUpdateCmd { get; }
        [Reactive] public bool EnableCheckPreReleaseUpdate { get; set; }
        [Reactive] public bool IsCheckUpdate { get; set; }
        [Reactive] public bool AutoRun { get; set; }

        public CheckUpdateViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = LazyConfig.Instance.Config;
            _updateView = updateView;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();

            RefreshSubItems();

            CheckUpdateCmd = ReactiveCommand.Create(() =>
            {
                CheckUpdate();
            });
            EnableCheckPreReleaseUpdate = _config.guiItem.checkPreReleaseUpdate;
            IsCheckUpdate = true;

            this.WhenAnyValue(
            x => x.EnableCheckPreReleaseUpdate,
            y => y == true)
                .Subscribe(c => { _config.guiItem.checkPreReleaseUpdate = EnableCheckPreReleaseUpdate; });
        }

        private void RefreshSubItems()
        {
            _checkUpdateItem.Clear();

            _checkUpdateItem.Add(new CheckUpdateItem()
            {
                isSelected = false,
                coreType = ECoreType.v2rayN.ToString(),
                remarks = ResUI.menuCheckUpdate,
            });
            _checkUpdateItem.Add(new CheckUpdateItem()
            {
                isSelected = true,
                coreType = ECoreType.Xray.ToString(),
                remarks = ResUI.menuCheckUpdate,
            });
            _checkUpdateItem.Add(new CheckUpdateItem()
            {
                isSelected = true,
                coreType = ECoreType.mihomo.ToString(),
                remarks = ResUI.menuCheckUpdate,
            });
            _checkUpdateItem.Add(new CheckUpdateItem()
            {
                isSelected = true,
                coreType = ECoreType.sing_box.ToString(),
                remarks = ResUI.menuCheckUpdate,
            });
            _checkUpdateItem.Add(new CheckUpdateItem()
            {
                isSelected = true,
                coreType = _geo,
                remarks = ResUI.menuCheckUpdate,
            });
        }

        private void CheckUpdate()
        {
            _lstUpdated.Clear();

            for (int k = _checkUpdateItem.Count - 1; k >= 0; k--)
            {
                var item = _checkUpdateItem[k];
                if (item.isSelected == true)
                {
                    IsCheckUpdate = false;
                    _lstUpdated.Add(new CheckUpdateItem() { coreType = item.coreType });
                    UpdateView(item.coreType, "...");
                    if (item.coreType == _geo)
                    {
                        CheckUpdateGeo();
                    }
                    else if (item.coreType == ECoreType.v2rayN.ToString())
                    {
                        CheckUpdateN(EnableCheckPreReleaseUpdate);
                    }
                    else if (item.coreType == ECoreType.mihomo.ToString())
                    {
                        CheckUpdateCore(item, false);
                    }
                    else
                    {
                        CheckUpdateCore(item, EnableCheckPreReleaseUpdate);
                    }
                }
            }
        }

        private void UpdatedPlusPlus(string coreType, string fileName)
        {
            var item = _lstUpdated.FirstOrDefault(x => x.coreType == coreType);
            if (item == null)
            {
                return;
            }
            item.isFinished = true;
            if (!fileName.IsNullOrEmpty())
            {
                item.fileName = fileName;
            }
        }

        private void CheckUpdateGeo()
        {
            void _updateUI(bool success, string msg)
            {
                UpdateView(_geo, msg);
                if (success)
                {
                    UpdatedPlusPlus(_geo, "");
                    UpdateFinished();
                }
            }
            (new UpdateHandler()).UpdateGeoFileAll(_config, _updateUI);
        }

        private void CheckUpdateN(bool preRelease)
        {
            //Check for standalone windows .Net version
            if (Utils.IsWindows()
                && File.Exists(Path.Combine(Utils.StartupPath(), "wpfgfx_cor3.dll"))
                && File.Exists(Path.Combine(Utils.StartupPath(), "D3DCompiler_47_cor3.dll"))
                )
            {
                UpdateView(ResUI.UpdateStandalonePackageTip, ResUI.UpdateStandalonePackageTip);
                return;
            }

            void _updateUI(bool success, string msg)
            {
                if (success)
                {
                    UpdateView(ECoreType.v2rayN.ToString(), ResUI.OperationSuccess);
                    UpdatedPlusPlus(ECoreType.v2rayN.ToString(), msg);
                    UpdateFinished();
                }
                else
                {
                    if (msg.IsNullOrEmpty())
                    {
                        UpdatedPlusPlus(ECoreType.v2rayN.ToString(), "");
                        UpdateFinished();
                    }
                    else
                    {
                        UpdateView(ECoreType.v2rayN.ToString(), msg);
                    }
                }
            }
           (new UpdateHandler()).CheckUpdateGuiN(_config, _updateUI, preRelease);
        }

        private void CheckUpdateCore(CheckUpdateItem item, bool preRelease)
        {
            void _updateUI(bool success, string msg)
            {
                if (success)
                {
                    UpdateView(item.coreType, ResUI.MsgUpdateV2rayCoreSuccessfullyMore);

                    UpdatedPlusPlus(item.coreType, msg);
                    UpdateFinished();
                }
                else
                {
                    if (msg.IsNullOrEmpty())
                    {
                        UpdatedPlusPlus(item.coreType, "");
                        UpdateFinished();
                    }
                    else
                    {
                        UpdateView(item.coreType, msg);
                    }
                }
            }
            var type = (ECoreType)Enum.Parse(typeof(ECoreType), item.coreType);
            (new UpdateHandler()).CheckUpdateCore(type, _config, _updateUI, preRelease);
        }

        private void UpdateFinished()
        {
            if (_lstUpdated.Count > 0 && _lstUpdated.Count(x => x.isFinished == true) == _lstUpdated.Count)
            {
                _updateView?.Invoke(EViewAction.DispatcherCheckUpdateFinished, false);

                UpgradeCore();

                if (_lstUpdated.Any(x => x.coreType == ECoreType.v2rayN.ToString() && x.isFinished == true))
                {
                    UpgradeN();
                }
                _updateView?.Invoke(EViewAction.DispatcherCheckUpdateFinished, true);
            }
        }

        public void UpdateFinishedResult(bool blReload)
        {
            if (blReload)
            {
                IsCheckUpdate = true;
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
                var fileName = _lstUpdated.FirstOrDefault(x => x.coreType == ECoreType.v2rayN.ToString())?.fileName;
                if (fileName.IsNullOrEmpty())
                {
                    return;
                }

                Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "v2rayUpgrade",
                        Arguments = fileName.AppendQuotes(),
                        WorkingDirectory = Utils.StartupPath()
                    }
                };
                process.Start();
                if (process.Id > 0)
                {
                    Locator.Current.GetService<MainWindowViewModel>()?.MyAppExitAsync(false);
                }
            }
            catch (Exception ex)
            {
                UpdateView(ECoreType.v2rayN.ToString(), ex.Message);
            }
        }

        private void UpgradeCore()
        {
            foreach (var item in _lstUpdated)
            {
                if (item.fileName.IsNullOrEmpty())
                {
                    continue;
                }

                var fileName = Utils.GetTempPath(Utils.GetDownloadFileName(item.fileName));
                if (!File.Exists(fileName))
                {
                    continue;
                }
                string toPath = Utils.GetBinPath("", item.coreType);

                if (fileName.Contains(".tar.gz"))
                {
                    //It's too complicated to unzip. TODO
                }
                else if (fileName.Contains(".gz"))
                {
                    FileManager.UncompressedFile(fileName, toPath, item.coreType);
                }
                else
                {
                    FileManager.ZipExtractToFile(fileName, toPath, _config.guiItem.ignoreGeoUpdateCore ? "geo" : "");
                }

                UpdateView(item.coreType, ResUI.MsgUpdateV2rayCoreSuccessfully);

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        private void UpdateView(string coreType, string msg)
        {
            var item = new CheckUpdateItem()
            {
                coreType = coreType,
                remarks = msg,
            };
            _updateView?.Invoke(EViewAction.DispatcherCheckUpdate, item);
        }

        public void UpdateViewResult(CheckUpdateItem item)
        {
            var found = _checkUpdateItem.FirstOrDefault(t => t.coreType == item.coreType);
            if (found != null)
            {
                var itemCopy = JsonUtils.DeepCopy(found);
                itemCopy.remarks = item.remarks;
                _checkUpdateItem.Replace(found, itemCopy);
            }
        }
    }
}