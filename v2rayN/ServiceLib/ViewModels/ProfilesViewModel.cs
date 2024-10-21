using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;

namespace ServiceLib.ViewModels
{
    public class ProfilesViewModel : MyReactiveObject
    {
        #region private prop

        private List<ProfileItem> _lstProfile;
        private string _serverFilter = string.Empty;
        private Dictionary<string, bool> _dicHeaderSort = new();
        private SpeedtestService? _speedtestHandler;

        #endregion private prop

        #region ObservableCollection

        private IObservableCollection<ProfileItemModel> _profileItems = new ObservableCollectionExtended<ProfileItemModel>();
        public IObservableCollection<ProfileItemModel> ProfileItems => _profileItems;

        private IObservableCollection<SubItem> _subItems = new ObservableCollectionExtended<SubItem>();
        public IObservableCollection<SubItem> SubItems => _subItems;

        private IObservableCollection<ComboItem> _servers = new ObservableCollectionExtended<ComboItem>();

        [Reactive]
        public ProfileItemModel SelectedProfile { get; set; }

        public IList<ProfileItemModel> SelectedProfiles { get; set; }

        [Reactive]
        public SubItem SelectedSub { get; set; }

        [Reactive]
        public SubItem SelectedMoveToGroup { get; set; }

        [Reactive]
        public ComboItem SelectedServer { get; set; }

        [Reactive]
        public string ServerFilter { get; set; }

        [Reactive]
        public bool BlServers { get; set; }

        #endregion ObservableCollection

        #region Menu

        //servers delete
        public ReactiveCommand<Unit, Unit> EditServerCmd { get; }

        public ReactiveCommand<Unit, Unit> RemoveServerCmd { get; }
        public ReactiveCommand<Unit, Unit> RemoveDuplicateServerCmd { get; }
        public ReactiveCommand<Unit, Unit> CopyServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SetDefaultServerCmd { get; }
        public ReactiveCommand<Unit, Unit> ShareServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SetDefaultMultipleServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SetDefaultLoadBalanceServerCmd { get; }

        //servers move
        public ReactiveCommand<Unit, Unit> MoveTopCmd { get; }

        public ReactiveCommand<Unit, Unit> MoveUpCmd { get; }
        public ReactiveCommand<Unit, Unit> MoveDownCmd { get; }
        public ReactiveCommand<Unit, Unit> MoveBottomCmd { get; }

        //servers ping
        public ReactiveCommand<Unit, Unit> MixedTestServerCmd { get; }

        public ReactiveCommand<Unit, Unit> TcpingServerCmd { get; }
        public ReactiveCommand<Unit, Unit> RealPingServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SpeedServerCmd { get; }
        public ReactiveCommand<Unit, Unit> SortServerResultCmd { get; }

        //servers export
        public ReactiveCommand<Unit, Unit> Export2ClientConfigCmd { get; }

        public ReactiveCommand<Unit, Unit> Export2ClientConfigClipboardCmd { get; }
        public ReactiveCommand<Unit, Unit> Export2ShareUrlCmd { get; }
        public ReactiveCommand<Unit, Unit> Export2ShareUrlBase64Cmd { get; }

        public ReactiveCommand<Unit, Unit> AddSubCmd { get; }
        public ReactiveCommand<Unit, Unit> EditSubCmd { get; }

        #endregion Menu

        #region Init

        public ProfilesViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;

            #region WhenAnyValue && ReactiveCommand

            var canEditRemove = this.WhenAnyValue(
               x => x.SelectedProfile,
               selectedSource => selectedSource != null && !selectedSource.indexId.IsNullOrEmpty());

            this.WhenAnyValue(
                x => x.SelectedSub,
                y => y != null && !y.remarks.IsNullOrEmpty() && _config.subIndexId != y.id)
                    .Subscribe(async c => await SubSelectedChangedAsync(c));
            this.WhenAnyValue(
                 x => x.SelectedMoveToGroup,
                 y => y != null && !y.remarks.IsNullOrEmpty())
                     .Subscribe(async c => await MoveToGroup(c));

            this.WhenAnyValue(
              x => x.SelectedServer,
              y => y != null && !y.Text.IsNullOrEmpty())
                  .Subscribe(async c => await ServerSelectedChanged(c));

            this.WhenAnyValue(
              x => x.ServerFilter,
              y => y != null && _serverFilter != y)
                  .Subscribe(c => ServerFilterChanged(c));

            //servers delete
            EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await EditServerAsync(EConfigType.Custom);
            }, canEditRemove);
            RemoveServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RemoveServerAsync();
            }, canEditRemove);
            RemoveDuplicateServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RemoveDuplicateServer();
            });
            CopyServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await CopyServer();
            }, canEditRemove);
            SetDefaultServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetDefaultServer();
            }, canEditRemove);
            ShareServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ShareServerAsync();
            }, canEditRemove);
            SetDefaultMultipleServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetDefaultMultipleServer(ECoreType.sing_box);
            }, canEditRemove);
            SetDefaultLoadBalanceServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SetDefaultMultipleServer(ECoreType.Xray);
            }, canEditRemove);

            //servers move
            MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveServer(EMove.Top);
            }, canEditRemove);
            MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveServer(EMove.Up);
            }, canEditRemove);
            MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveServer(EMove.Down);
            }, canEditRemove);
            MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveServer(EMove.Bottom);
            }, canEditRemove);

            //servers ping
            MixedTestServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ServerSpeedtest(ESpeedActionType.Mixedtest);
            });
            TcpingServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ServerSpeedtest(ESpeedActionType.Tcping);
            }, canEditRemove);
            RealPingServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ServerSpeedtest(ESpeedActionType.Realping);
            }, canEditRemove);
            SpeedServerCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ServerSpeedtest(ESpeedActionType.Speedtest);
            }, canEditRemove);
            SortServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SortServer(EServerColName.delayVal.ToString());
            });
            //servers export
            Export2ClientConfigCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await Export2ClientConfigAsync(false);
            }, canEditRemove);
            Export2ClientConfigClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await Export2ClientConfigAsync(true);
            }, canEditRemove);
            Export2ShareUrlCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await Export2ShareUrlAsync(false);
            }, canEditRemove);
            Export2ShareUrlBase64Cmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await Export2ShareUrlAsync(true);
            }, canEditRemove);

            //Subscription
            AddSubCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await EditSubAsync(true);
            });
            EditSubCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await EditSubAsync(false);
            });

            #endregion WhenAnyValue && ReactiveCommand

            if (_updateView != null)
            {
                MessageBus.Current.Listen<string>(EMsgCommand.RefreshProfiles.ToString()).Subscribe(OnNext);
            }

            Init();
        }

        private async Task Init()
        {
            SelectedProfile = new();
            SelectedSub = new();
            SelectedMoveToGroup = new();
            SelectedServer = new();

            await RefreshSubscriptions();
            RefreshServers();
        }

        #endregion Init

        #region Actions

        private async void OnNext(string x)
        {
            await _updateView?.Invoke(EViewAction.DispatcherRefreshServersBiz, null);
        }

        private void Reload()
        {
            Locator.Current.GetService<MainWindowViewModel>()?.Reload();
        }

        private void UpdateSpeedtestHandler(SpeedTestResult result)
        {
            _updateView?.Invoke(EViewAction.DispatcherSpeedTest, result);
        }

        public void SetSpeedTestResult(SpeedTestResult result)
        {
            if (Utils.IsNullOrEmpty(result.IndexId))
            {
                NoticeHandler.Instance.SendMessageEx(result.Delay);
                NoticeHandler.Instance.Enqueue(result.Delay);
                return;
            }
            var item = _profileItems.Where(it => it.indexId == result.IndexId).FirstOrDefault();
            if (item != null)
            {
                if (Utils.IsNotEmpty(result.Delay))
                {
                    int.TryParse(result.Delay, out int temp);
                    item.delay = temp;
                    item.delayVal = $"{result.Delay} {Global.DelayUnit}";
                }
                if (Utils.IsNotEmpty(result.Speed))
                {
                    item.speedVal = $"{result.Speed} {Global.SpeedUnit}";
                }
                _profileItems.Replace(item, JsonUtils.DeepCopy(item));
            }
        }

        public void UpdateStatistics(ServerSpeedItem update)
        {
            try
            {
                var item = _profileItems.Where(it => it.indexId == update.indexId).FirstOrDefault();
                if (item != null)
                {
                    item.todayDown = Utils.HumanFy(update.todayDown);
                    item.todayUp = Utils.HumanFy(update.todayUp);
                    item.totalDown = Utils.HumanFy(update.totalDown);
                    item.totalUp = Utils.HumanFy(update.totalUp);

                    if (SelectedProfile?.indexId == item.indexId)
                    {
                        var temp = JsonUtils.DeepCopy(item);
                        _profileItems.Replace(item, temp);
                        SelectedProfile = temp;
                    }
                    else
                    {
                        _profileItems.Replace(item, JsonUtils.DeepCopy(item));
                    }
                }
            }
            catch
            {
            }
        }

        public async Task AutofitColumnWidthAsync()
        {
            await _updateView?.Invoke(EViewAction.AdjustMainLvColWidth, null);
        }

        #endregion Actions

        #region Servers && Groups

        private async Task SubSelectedChangedAsync(bool c)
        {
            if (!c)
            {
                return;
            }
            _config.subIndexId = SelectedSub?.id;

            RefreshServers();

            await _updateView?.Invoke(EViewAction.ProfilesFocus, null);
        }

        private void ServerFilterChanged(bool c)
        {
            if (!c)
            {
                return;
            }
            _serverFilter = ServerFilter;
            if (Utils.IsNullOrEmpty(_serverFilter))
            {
                RefreshServers();
            }
        }

        public void RefreshServers()
        {
            MessageBus.Current.SendMessage("", EMsgCommand.RefreshProfiles.ToString());
        }

        public async Task RefreshServersBiz()
        {
            var lstModel = await AppHandler.Instance.ProfileItemsEx(_config.subIndexId, _serverFilter);
            _lstProfile = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(lstModel)) ?? [];

            _profileItems.Clear();
            _profileItems.AddRange(lstModel);
            if (lstModel.Count > 0)
            {
                var selected = lstModel.FirstOrDefault(t => t.indexId == _config.indexId);
                if (selected != null)
                {
                    SelectedProfile = selected;
                }
                else
                {
                    SelectedProfile = lstModel[0];
                }
            }
        }

        public async Task RefreshSubscriptions()
        {
            _subItems.Clear();

            _subItems.Add(new SubItem { remarks = ResUI.AllGroupServers });

            foreach (var item in await AppHandler.Instance.SubItems())
            {
                _subItems.Add(item);
            }
            if (_config.subIndexId != null && _subItems.FirstOrDefault(t => t.id == _config.subIndexId) != null)
            {
                SelectedSub = _subItems.FirstOrDefault(t => t.id == _config.subIndexId);
            }
            else
            {
                SelectedSub = _subItems[0];
            }
        }

        #endregion Servers && Groups

        #region Add Servers

        private async Task<List<ProfileItem>?> GetProfileItems(bool latest)
        {
            var lstSelecteds = new List<ProfileItem>();
            if (SelectedProfiles == null || SelectedProfiles.Count <= 0)
            {
                return null;
            }

            var orderProfiles = SelectedProfiles?.OrderBy(t => t.sort);
            if (latest)
            {
                foreach (var profile in orderProfiles)
                {
                    var item = await AppHandler.Instance.GetProfileItem(profile.indexId);
                    if (item is not null)
                    {
                        lstSelecteds.Add(item);
                    }
                }
            }
            else
            {
                lstSelecteds = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(orderProfiles));
            }

            return lstSelecteds;
        }

        public async Task EditServerAsync(EConfigType eConfigType)
        {
            if (Utils.IsNullOrEmpty(SelectedProfile?.indexId))
            {
                return;
            }
            var item = await AppHandler.Instance.GetProfileItem(SelectedProfile.indexId);
            if (item is null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectServer);
                return;
            }
            eConfigType = item.configType;

            bool? ret = false;
            if (eConfigType == EConfigType.Custom)
            {
                ret = await _updateView?.Invoke(EViewAction.AddServer2Window, item);
            }
            else
            {
                ret = await _updateView?.Invoke(EViewAction.AddServerWindow, item);
            }
            if (ret == true)
            {
                RefreshServers();
                if (item.indexId == _config.indexId)
                {
                    Reload();
                }
            }
        }

        public async Task RemoveServerAsync()
        {
            var lstSelecteds = await GetProfileItems(true);
            if (lstSelecteds == null)
            {
                return;
            }
            if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
            {
                return;
            }
            var exists = lstSelecteds.Exists(t => t.indexId == _config.indexId);

            await ConfigHandler.RemoveServer(_config, lstSelecteds);
            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);

            RefreshServers();
            if (exists)
            {
                Reload();
            }
        }

        private async Task RemoveDuplicateServer()
        {
            var tuple = await ConfigHandler.DedupServerList(_config, _config.subIndexId);
            RefreshServers();
            Reload();
            NoticeHandler.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2));
        }

        private async Task CopyServer()
        {
            var lstSelecteds = await GetProfileItems(false);
            if (lstSelecteds == null)
            {
                return;
            }
            if (await ConfigHandler.CopyServer(_config, lstSelecteds) == 0)
            {
                RefreshServers();
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            }
        }

        public async Task SetDefaultServer()
        {
            if (Utils.IsNullOrEmpty(SelectedProfile?.indexId))
            {
                return;
            }
            await SetDefaultServer(SelectedProfile.indexId);
        }

        public async Task SetDefaultServer(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return;
            }
            if (indexId == _config.indexId)
            {
                return;
            }
            var item = await AppHandler.Instance.GetProfileItem(indexId);
            if (item is null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectServer);
                return;
            }

            if (await ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
            {
                RefreshServers();
                Reload();
            }
        }

        private async Task ServerSelectedChanged(bool c)
        {
            if (!c)
            {
                return;
            }
            if (SelectedServer == null)
            {
                return;
            }
            if (Utils.IsNullOrEmpty(SelectedServer.ID))
            {
                return;
            }
            await SetDefaultServer(SelectedServer.ID);
        }

        public async Task ShareServerAsync()
        {
            var item = await AppHandler.Instance.GetProfileItem(SelectedProfile.indexId);
            if (item is null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectServer);
                return;
            }
            var url = FmtHandler.GetShareUri(item);
            if (Utils.IsNullOrEmpty(url))
            {
                return;
            }

            await _updateView?.Invoke(EViewAction.ShareServer, url);
        }

        private async Task SetDefaultMultipleServer(ECoreType coreType)
        {
            var lstSelecteds = await GetProfileItems(true);
            if (lstSelecteds == null)
            {
                return;
            }

            var ret = await ConfigHandler.AddCustomServer4Multiple(_config, lstSelecteds, coreType);
            if (ret.Success != true)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
                return;
            }
            if (ret?.Data?.ToString() == _config.indexId)
            {
                RefreshServers();
                Reload();
            }
            else
            {
                await SetDefaultServer(ret?.Data?.ToString());
            }
        }

        public async Task SortServer(string colName)
        {
            if (Utils.IsNullOrEmpty(colName))
            {
                return;
            }

            _dicHeaderSort.TryAdd(colName, true);
            _dicHeaderSort.TryGetValue(colName, out bool asc);
            if (await ConfigHandler.SortServers(_config, _config.subIndexId, colName, asc) != 0)
            {
                return;
            }
            _dicHeaderSort[colName] = !asc;
            RefreshServers();
        }

        //move server
        private async Task MoveToGroup(bool c)
        {
            if (!c)
            {
                return;
            }

            var lstSelecteds = await GetProfileItems(true);
            if (lstSelecteds == null)
            {
                return;
            }

            await ConfigHandler.MoveToGroup(_config, lstSelecteds, SelectedMoveToGroup.id);
            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);

            RefreshServers();
            SelectedMoveToGroup = new();
            //Reload();
        }

        public async Task MoveServer(EMove eMove)
        {
            var item = _lstProfile.FirstOrDefault(t => t.indexId == SelectedProfile.indexId);
            if (item is null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectServer);
                return;
            }

            int index = _lstProfile.IndexOf(item);
            if (index < 0)
            {
                return;
            }
            if (await ConfigHandler.MoveServer(_config, _lstProfile, index, eMove) == 0)
            {
                RefreshServers();
            }
        }

        public async Task MoveServerTo(int startIndex, ProfileItemModel targetItem)
        {
            var targetIndex = _profileItems.IndexOf(targetItem);
            if (startIndex >= 0 && targetIndex >= 0 && startIndex != targetIndex)
            {
                if (await ConfigHandler.MoveServer(_config, _lstProfile, startIndex, EMove.Position, targetIndex) == 0)
                {
                    RefreshServers();
                }
            }
        }

        public async Task ServerSpeedtest(ESpeedActionType actionType)
        {
            if (actionType == ESpeedActionType.Mixedtest)
            {
                SelectedProfiles = _profileItems;
            }
            var lstSelecteds = await GetProfileItems(false);
            if (lstSelecteds == null)
            {
                return;
            }
            //ClearTestResult();

            _speedtestHandler = new SpeedtestService(_config, lstSelecteds, actionType, UpdateSpeedtestHandler);
        }

        public void ServerSpeedtestStop()
        {
            _speedtestHandler?.ExitLoop();
        }

        private async Task Export2ClientConfigAsync(bool blClipboard)
        {
            var item = await AppHandler.Instance.GetProfileItem(SelectedProfile.indexId);
            if (item is null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectServer);
                return;
            }
            if (blClipboard)
            {
                var result = await CoreConfigHandler.GenerateClientConfig(item, null);
                if (result.Success != true)
                {
                    NoticeHandler.Instance.Enqueue(result.Msg);
                }
                else
                {
                    await _updateView?.Invoke(EViewAction.SetClipboardData, result.Data);
                    NoticeHandler.Instance.SendMessage(ResUI.OperationSuccess);
                }
            }
            else
            {
                await _updateView?.Invoke(EViewAction.SaveFileDialog, item);
            }
        }

        public async Task Export2ClientConfigResult(string fileName, ProfileItem item)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }
            var result = await CoreConfigHandler.GenerateClientConfig(item, null);
            if (result.Success != true)
            {
                NoticeHandler.Instance.Enqueue(result.Msg);
            }
            else
            {
                NoticeHandler.Instance.SendMessageAndEnqueue(string.Format(ResUI.SaveClientConfigurationIn, fileName));
            }
        }

        public async Task Export2ShareUrlAsync(bool blEncode)
        {
            var lstSelecteds = await GetProfileItems(true);
            if (lstSelecteds == null)
            {
                return;
            }

            StringBuilder sb = new();
            foreach (var it in lstSelecteds)
            {
                var url = FmtHandler.GetShareUri(it);
                if (Utils.IsNullOrEmpty(url))
                {
                    continue;
                }
                sb.Append(url);
                sb.AppendLine();
            }
            if (sb.Length > 0)
            {
                if (blEncode)
                {
                    await _updateView?.Invoke(EViewAction.SetClipboardData, Utils.Base64Encode(sb.ToString()));
                }
                else
                {
                    await _updateView?.Invoke(EViewAction.SetClipboardData, sb.ToString());
                }
                NoticeHandler.Instance.SendMessage(ResUI.BatchExportURLSuccessfully);
            }
        }

        #endregion Add Servers

        #region Subscription

        private async Task EditSubAsync(bool blNew)
        {
            SubItem item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = await AppHandler.Instance.GetSubItem(_config.subIndexId);
                if (item is null)
                {
                    return;
                }
            }
            if (await _updateView?.Invoke(EViewAction.SubEditWindow, item) == true)
            {
                await RefreshSubscriptions();
                await SubSelectedChangedAsync(true);
            }
        }

        #endregion Subscription
    }
}