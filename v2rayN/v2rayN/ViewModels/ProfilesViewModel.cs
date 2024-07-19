using DynamicData;
using DynamicData.Binding;
using MaterialDesignThemes.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Handler.Fmt;
using v2rayN.Handler.Statistics;
using v2rayN.Models;
using v2rayN.Resx;
using v2rayN.Views;

namespace v2rayN.ViewModels
{
    public class ProfilesViewModel : ReactiveObject
    {
        #region private prop

        private List<ProfileItem> _lstProfile;
        private string _serverFilter = string.Empty;
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Dictionary<string, bool> _dicHeaderSort = new();
        private Action<EViewAction> _updateView;

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

        public ReactiveCommand<Unit, Unit> Export2ShareUrlCmd { get; }

        public ReactiveCommand<Unit, Unit> AddSubCmd { get; }
        public ReactiveCommand<Unit, Unit> EditSubCmd { get; }

        #endregion Menu

        #region Init

        public ProfilesViewModel(Action<EViewAction> updateView)
        {
            _updateView = updateView;

            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _config = LazyConfig.Instance.GetConfig();
            MessageBus.Current.Listen<string>(Global.CommandRefreshProfiles).Subscribe(x => RefreshServersBiz());

            SelectedProfile = new();
            SelectedSub = new();
            SelectedMoveToGroup = new();
            SelectedServer = new();

            RefreshSubscriptions();
            RefreshServers();

            #region WhenAnyValue && ReactiveCommand

            var canEditRemove = this.WhenAnyValue(
               x => x.SelectedProfile,
               selectedSource => selectedSource != null && !selectedSource.indexId.IsNullOrEmpty());

            this.WhenAnyValue(
                x => x.SelectedSub,
                y => y != null && !y.remarks.IsNullOrEmpty() && _config.subIndexId != y.id)
                    .Subscribe(c => SubSelectedChanged(c));
            this.WhenAnyValue(
                 x => x.SelectedMoveToGroup,
                 y => y != null && !y.remarks.IsNullOrEmpty())
                     .Subscribe(c => MoveToGroup(c));

            this.WhenAnyValue(
              x => x.SelectedServer,
              y => y != null && !y.Text.IsNullOrEmpty())
                  .Subscribe(c => ServerSelectedChanged(c));

            this.WhenAnyValue(
              x => x.ServerFilter,
              y => y != null && _serverFilter != y)
                  .Subscribe(c => ServerFilterChanged(c));

            //servers delete
            EditServerCmd = ReactiveCommand.Create(() =>
            {
                EditServer(false, EConfigType.Custom);
            }, canEditRemove);
            RemoveServerCmd = ReactiveCommand.Create(() =>
            {
                RemoveServer();
            }, canEditRemove);
            RemoveDuplicateServerCmd = ReactiveCommand.Create(() =>
            {
                RemoveDuplicateServer();
            });
            CopyServerCmd = ReactiveCommand.Create(() =>
            {
                CopyServer();
            }, canEditRemove);
            SetDefaultServerCmd = ReactiveCommand.Create(() =>
            {
                SetDefaultServer();
            }, canEditRemove);
            ShareServerCmd = ReactiveCommand.Create(() =>
            {
                ShareServer();
            }, canEditRemove);
            SetDefaultMultipleServerCmd = ReactiveCommand.Create(() =>
            {
                SetDefaultMultipleServer();
            }, canEditRemove);
            //servers move
            MoveTopCmd = ReactiveCommand.Create(() =>
            {
                MoveServer(EMove.Top);
            }, canEditRemove);
            MoveUpCmd = ReactiveCommand.Create(() =>
            {
                MoveServer(EMove.Up);
            }, canEditRemove);
            MoveDownCmd = ReactiveCommand.Create(() =>
            {
                MoveServer(EMove.Down);
            }, canEditRemove);
            MoveBottomCmd = ReactiveCommand.Create(() =>
            {
                MoveServer(EMove.Bottom);
            }, canEditRemove);

            //servers ping
            MixedTestServerCmd = ReactiveCommand.Create(() =>
            {
                ServerSpeedtest(ESpeedActionType.Mixedtest);
            });
            TcpingServerCmd = ReactiveCommand.Create(() =>
            {
                ServerSpeedtest(ESpeedActionType.Tcping);
            }, canEditRemove);
            RealPingServerCmd = ReactiveCommand.Create(() =>
            {
                ServerSpeedtest(ESpeedActionType.Realping);
            }, canEditRemove);
            SpeedServerCmd = ReactiveCommand.Create(() =>
            {
                ServerSpeedtest(ESpeedActionType.Speedtest);
            }, canEditRemove);
            SortServerResultCmd = ReactiveCommand.Create(() =>
            {
                SortServer(EServerColName.delayVal.ToString());
            });
            //servers export
            Export2ClientConfigCmd = ReactiveCommand.Create(() =>
            {
                Export2ClientConfig();
            }, canEditRemove);
            Export2ShareUrlCmd = ReactiveCommand.Create(() =>
            {
                Export2ShareUrl();
            }, canEditRemove);

            //Subscription
            AddSubCmd = ReactiveCommand.Create(() =>
            {
                EditSub(true);
            });
            EditSubCmd = ReactiveCommand.Create(() =>
            {
                EditSub(false);
            });

            #endregion WhenAnyValue && ReactiveCommand
        }

        #endregion Init

        #region Actions

        private void Reload()
        {
            Locator.Current.GetService<MainWindowViewModel>()?.Reload();
        }

        private void UpdateSpeedtestHandler(string indexId, string delay, string speed)
        {
            Application.Current?.Dispatcher.Invoke((Action)(() =>
            {
                SetTestResult(indexId, delay, speed);
            }));
        }

        private void SetTestResult(string indexId, string delay, string speed)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                _noticeHandler?.SendMessage(delay, true);
                _noticeHandler?.Enqueue(delay);
                return;
            }
            var item = _profileItems.Where(it => it.indexId == indexId).FirstOrDefault();
            if (item != null)
            {
                if (!Utils.IsNullOrEmpty(delay))
                {
                    int.TryParse(delay, out int temp);
                    item.delay = temp;
                    item.delayVal = $"{delay} {Global.DelayUnit}";
                }
                if (!Utils.IsNullOrEmpty(speed))
                {
                    item.speedVal = $"{speed} {Global.SpeedUnit}";
                }
                _profileItems.Replace(item, JsonUtils.DeepCopy(item));
            }
        }

        public void UpdateStatistics(ServerSpeedItem update)
        {
            try
            {
                Application.Current?.Dispatcher.Invoke((Action)(() =>
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
                }));
            }
            catch
            {
            }
        }

        #endregion Actions

        #region Servers && Groups

        private void SubSelectedChanged(bool c)
        {
            if (!c)
            {
                return;
            }
            _config.subIndexId = SelectedSub?.id;

            RefreshServers();

            _updateView(EViewAction.ProfilesFocus);
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
            MessageBus.Current.SendMessage("", Global.CommandRefreshProfiles);
        }

        private void RefreshServersBiz()
        {
            var lstModel = LazyConfig.Instance.ProfileItems(_config.subIndexId, _serverFilter);

            ConfigHandler.SetDefaultServer(_config, lstModel);

            var lstServerStat = (_config.guiItem.enableStatistics ? StatisticsHandler.Instance.ServerStat : null) ?? [];
            var lstProfileExs = ProfileExHandler.Instance.ProfileExs;
            lstModel = (from t in lstModel
                        join t2 in lstServerStat on t.indexId equals t2.indexId into t2b
                        from t22 in t2b.DefaultIfEmpty()
                        join t3 in lstProfileExs on t.indexId equals t3.indexId into t3b
                        from t33 in t3b.DefaultIfEmpty()
                        select new ProfileItemModel
                        {
                            indexId = t.indexId,
                            configType = t.configType,
                            remarks = t.remarks,
                            address = t.address,
                            port = t.port,
                            security = t.security,
                            network = t.network,
                            streamSecurity = t.streamSecurity,
                            subid = t.subid,
                            subRemarks = t.subRemarks,
                            isActive = t.indexId == _config.indexId,
                            sort = t33 == null ? 0 : t33.sort,
                            delay = t33 == null ? 0 : t33.delay,
                            delayVal = t33?.delay != 0 ? $"{t33?.delay} {Global.DelayUnit}" : string.Empty,
                            speedVal = t33?.speed != 0 ? $"{t33?.speed} {Global.SpeedUnit}" : string.Empty,
                            todayDown = t22 == null ? "" : Utils.HumanFy(t22.todayDown),
                            todayUp = t22 == null ? "" : Utils.HumanFy(t22.todayUp),
                            totalDown = t22 == null ? "" : Utils.HumanFy(t22.totalDown),
                            totalUp = t22 == null ? "" : Utils.HumanFy(t22.totalUp)
                        }).OrderBy(t => t.sort).ToList();
            _lstProfile = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(lstModel));

            Application.Current?.Dispatcher.Invoke((Action)(() =>
            {
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
            }));
        }

        public void RefreshSubscriptions()
        {
            _subItems.Clear();

            _subItems.Add(new SubItem { remarks = ResUI.AllGroupServers });
            foreach (var item in LazyConfig.Instance.SubItems().OrderBy(t => t.sort))
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

        private int GetProfileItems(out List<ProfileItem> lstSelecteds, bool latest)
        {
            lstSelecteds = new List<ProfileItem>();
            if (SelectedProfiles == null || SelectedProfiles.Count <= 0)
            {
                return -1;
            }

            var orderProfiles = SelectedProfiles?.OrderBy(t => t.sort);
            if (latest)
            {
                foreach (var profile in orderProfiles)
                {
                    var item = LazyConfig.Instance.GetProfileItem(profile.indexId);
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

            return 0;
        }

        public void EditServer(bool blNew, EConfigType eConfigType)
        {
            ProfileItem item;
            if (blNew)
            {
                item = new()
                {
                    subid = _config.subIndexId,
                    configType = eConfigType,
                    isSub = false,
                };
            }
            else
            {
                if (Utils.IsNullOrEmpty(SelectedProfile?.indexId))
                {
                    return;
                }
                item = LazyConfig.Instance.GetProfileItem(SelectedProfile.indexId);
                if (item is null)
                {
                    _noticeHandler?.Enqueue(ResUI.PleaseSelectServer);
                    return;
                }
                eConfigType = item.configType;
            }
            bool? ret = false;
            if (eConfigType == EConfigType.Custom)
            {
                ret = (new AddServer2Window(item)).ShowDialog();
            }
            else
            {
                ret = (new AddServerWindow(item)).ShowDialog();
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

        public void RemoveServer()
        {
            if (GetProfileItems(out List<ProfileItem> lstSelecteds, true) < 0)
            {
                return;
            }

            if (UI.ShowYesNo(ResUI.RemoveServer) == MessageBoxResult.No)
            {
                return;
            }
            var exists = lstSelecteds.Exists(t => t.indexId == _config.indexId);

            ConfigHandler.RemoveServer(_config, lstSelecteds);
            _noticeHandler?.Enqueue(ResUI.OperationSuccess);

            RefreshServers();
            if (exists)
            {
                Reload();
            }
        }

        private void RemoveDuplicateServer()
        {
            var tuple = ConfigHandler.DedupServerList(_config, _config.subIndexId);
            RefreshServers();
            Reload();
            _noticeHandler?.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2));
        }

        private void CopyServer()
        {
            if (GetProfileItems(out List<ProfileItem> lstSelecteds, false) < 0)
            {
                return;
            }
            if (ConfigHandler.CopyServer(_config, lstSelecteds) == 0)
            {
                RefreshServers();
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            }
        }

        public void SetDefaultServer()
        {
            if (Utils.IsNullOrEmpty(SelectedProfile?.indexId))
            {
                return;
            }
            SetDefaultServer(SelectedProfile.indexId);
        }

        private void SetDefaultServer(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return;
            }
            if (indexId == _config.indexId)
            {
                return;
            }
            var item = LazyConfig.Instance.GetProfileItem(indexId);
            if (item is null)
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectServer);
                return;
            }

            if (ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
            {
                RefreshServers();
                Reload();
            }
        }

        private void ServerSelectedChanged(bool c)
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
            SetDefaultServer(SelectedServer.ID);
        }

        public async void ShareServer()
        {
            var item = LazyConfig.Instance.GetProfileItem(SelectedProfile.indexId);
            if (item is null)
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectServer);
                return;
            }
            var url = FmtHandler.GetShareUri(item);
            if (Utils.IsNullOrEmpty(url))
            {
                return;
            }
            var img = QRCodeHelper.GetQRCode(url);
            var dialog = new QrcodeView()
            {
                imgQrcode = { Source = img },
                txtContent = { Text = url },
            };

            await DialogHost.Show(dialog, "RootDialog");
        }

        private void SetDefaultMultipleServer()
        {
            if (GetProfileItems(out List<ProfileItem> lstSelecteds, true) < 0)
            {
                return;
            }

            if (ConfigHandler.AddCustomServer4Multiple(_config, lstSelecteds, out string indexId) != 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
                return;
            }
            if (indexId == _config.indexId)
            {
                Reload();
            }
            else
            {
                SetDefaultServer(indexId);
            }
        }

        public void SortServer(string colName)
        {
            if (Utils.IsNullOrEmpty(colName))
            {
                return;
            }

            _dicHeaderSort.TryAdd(colName, true);
            _dicHeaderSort.TryGetValue(colName, out bool asc);
            if (ConfigHandler.SortServers(_config, _config.subIndexId, colName, asc) != 0)
            {
                return;
            }
            _dicHeaderSort[colName] = !asc;
            RefreshServers();
        }

        //move server
        private void MoveToGroup(bool c)
        {
            if (!c)
            {
                return;
            }

            if (GetProfileItems(out List<ProfileItem> lstSelecteds, true) < 0)
            {
                return;
            }

            ConfigHandler.MoveToGroup(_config, lstSelecteds, SelectedMoveToGroup.id);
            _noticeHandler?.Enqueue(ResUI.OperationSuccess);

            RefreshServers();
            SelectedMoveToGroup = new();
            //Reload();
        }

        public void MoveServer(EMove eMove)
        {
            var item = _lstProfile.FirstOrDefault(t => t.indexId == SelectedProfile.indexId);
            if (item is null)
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectServer);
                return;
            }

            int index = _lstProfile.IndexOf(item);
            if (index < 0)
            {
                return;
            }
            if (ConfigHandler.MoveServer(_config, ref _lstProfile, index, eMove) == 0)
            {
                RefreshServers();
            }
        }

        public void MoveServerTo(int startIndex, ProfileItemModel targetItem)
        {
            var targetIndex = _profileItems.IndexOf(targetItem);
            if (startIndex >= 0 && targetIndex >= 0 && startIndex != targetIndex)
            {
                if (ConfigHandler.MoveServer(_config, ref _lstProfile, startIndex, EMove.Position, targetIndex) == 0)
                {
                    RefreshServers();
                }
            }
        }

        public void ServerSpeedtest(ESpeedActionType actionType)
        {
            if (actionType == ESpeedActionType.Mixedtest)
            {
                SelectedProfiles = _profileItems;
            }
            if (GetProfileItems(out List<ProfileItem> lstSelecteds, false) < 0)
            {
                return;
            }
            //ClearTestResult();
            var coreHandler = Locator.Current.GetService<CoreHandler>();
            if (coreHandler != null)
            {
                new SpeedtestHandler(_config, coreHandler, lstSelecteds, actionType, UpdateSpeedtestHandler);
            }
        }

        private void Export2ClientConfig()
        {
            var item = LazyConfig.Instance.GetProfileItem(SelectedProfile.indexId);
            if (item is null)
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectServer);
                return;
            }
            MainFormHandler.Instance.Export2ClientConfig(item, _config);
        }

        public void Export2ShareUrl()
        {
            if (GetProfileItems(out List<ProfileItem> lstSelecteds, true) < 0)
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
                Utils.SetClipboardData(sb.ToString());
                _noticeHandler?.SendMessage(ResUI.BatchExportURLSuccessfully);
            }
        }

        #endregion Add Servers

        #region Subscription

        private void EditSub(bool blNew)
        {
            SubItem item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = LazyConfig.Instance.GetSubItem(_config.subIndexId);
                if (item is null)
                {
                    return;
                }
            }
            var ret = (new SubEditWindow(item)).ShowDialog();
            if (ret == true)
            {
                RefreshSubscriptions();
                SubSelectedChanged(true);
            }
        }

        #endregion Subscription
    }
}