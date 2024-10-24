using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class RoutingSettingViewModel : MyReactiveObject
    {
        private RoutingItem _lockedItem;
        private List<RulesItem> _lockedRules;

        #region Reactive

        private IObservableCollection<RoutingItemModel> _routingItems = new ObservableCollectionExtended<RoutingItemModel>();
        public IObservableCollection<RoutingItemModel> RoutingItems => _routingItems;

        [Reactive]
        public RoutingItemModel SelectedSource { get; set; }

        public IList<RoutingItemModel> SelectedSources { get; set; }

        [Reactive]
        public bool enableRoutingAdvanced { get; set; }

        [Reactive]
        public bool enableRoutingBasic { get; set; }

        [Reactive]
        public string domainStrategy { get; set; }

        [Reactive]
        public string domainMatcher { get; set; }

        [Reactive]
        public string domainStrategy4Singbox { get; set; }

        [Reactive]
        public string ProxyDomain { get; set; }

        [Reactive]
        public string ProxyIP { get; set; }

        [Reactive]
        public string DirectDomain { get; set; }

        [Reactive]
        public string DirectIP { get; set; }

        [Reactive]
        public string BlockDomain { get; set; }

        [Reactive]
        public string BlockIP { get; set; }

        public ReactiveCommand<Unit, Unit> RoutingBasicImportRulesCmd { get; }
        public ReactiveCommand<Unit, Unit> RoutingAdvancedAddCmd { get; }
        public ReactiveCommand<Unit, Unit> RoutingAdvancedRemoveCmd { get; }
        public ReactiveCommand<Unit, Unit> RoutingAdvancedSetDefaultCmd { get; }
        public ReactiveCommand<Unit, Unit> RoutingAdvancedImportRulesCmd { get; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }
        public bool IsModified { get; set; }

        #endregion Reactive

        public RoutingSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _updateView = updateView;

            var canEditRemove = this.WhenAnyValue(
                x => x.SelectedSource,
                selectedSource => selectedSource != null && !selectedSource.Remarks.IsNullOrEmpty());

            this.WhenAnyValue(
                    x => x.enableRoutingAdvanced)
                .Subscribe(c => enableRoutingBasic = !enableRoutingAdvanced);

            RoutingBasicImportRulesCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RoutingBasicImportRules();
            });

            RoutingAdvancedAddCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RoutingAdvancedEditAsync(true);
            });
            RoutingAdvancedRemoveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RoutingAdvancedRemoveAsync();
            }, canEditRemove);
            RoutingAdvancedSetDefaultCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RoutingAdvancedSetDefault();
            }, canEditRemove);
            RoutingAdvancedImportRulesCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RoutingAdvancedImportRules();
            });

            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveRoutingAsync();
            });

            Init();
        }

        private async Task Init()
        {
            SelectedSource = new();

            enableRoutingAdvanced = true;//TODO _config.RoutingBasicItem.EnableRoutingAdvanced;
            domainStrategy = _config.RoutingBasicItem.DomainStrategy;
            domainMatcher = _config.RoutingBasicItem.DomainMatcher;
            domainStrategy4Singbox = _config.RoutingBasicItem.DomainStrategy4Singbox;

            await ConfigHandler.InitBuiltinRouting(_config);
            await RefreshRoutingItems();
            await BindingLockedData();
        }

        #region locked

        private async Task BindingLockedData()
        {
            _lockedItem = await ConfigHandler.GetLockedRoutingItem(_config);
            if (_lockedItem == null)
            {
                _lockedItem = new RoutingItem()
                {
                    Remarks = "locked",
                    Url = string.Empty,
                    Locked = true,
                };
                await ConfigHandler.AddBatchRoutingRules(_lockedItem, Utils.GetEmbedText(Global.CustomRoutingFileName + "locked"));
            }

            if (_lockedItem != null)
            {
                _lockedRules = JsonUtils.Deserialize<List<RulesItem>>(_lockedItem.RuleSet);
                ProxyDomain = Utils.List2String(_lockedRules[0].Domain, true);
                ProxyIP = Utils.List2String(_lockedRules[0].Ip, true);

                DirectDomain = Utils.List2String(_lockedRules[1].Domain, true);
                DirectIP = Utils.List2String(_lockedRules[1].Ip, true);

                BlockDomain = Utils.List2String(_lockedRules[2].Domain, true);
                BlockIP = Utils.List2String(_lockedRules[2].Ip, true);
            }
        }

        private async Task EndBindingLockedData()
        {
            if (_lockedItem != null)
            {
                _lockedRules[0].Domain = Utils.String2List(Utils.Convert2Comma(ProxyDomain.TrimEx()));
                _lockedRules[0].Ip = Utils.String2List(Utils.Convert2Comma(ProxyIP.TrimEx()));

                _lockedRules[1].Domain = Utils.String2List(Utils.Convert2Comma(DirectDomain.TrimEx()));
                _lockedRules[1].Ip = Utils.String2List(Utils.Convert2Comma(DirectIP.TrimEx()));

                _lockedRules[2].Domain = Utils.String2List(Utils.Convert2Comma(BlockDomain.TrimEx()));
                _lockedRules[2].Ip = Utils.String2List(Utils.Convert2Comma(BlockIP.TrimEx()));

                _lockedItem.RuleSet = JsonUtils.Serialize(_lockedRules, false);

                await ConfigHandler.SaveRoutingItem(_config, _lockedItem);
            }
        }

        #endregion locked

        #region Refresh Save

        public async Task RefreshRoutingItems()
        {
            _routingItems.Clear();

            var routings = await AppHandler.Instance.RoutingItems();
            foreach (var item in routings)
            {
                bool def = false;
                if (item.Id == _config.RoutingBasicItem.RoutingIndexId)
                {
                    def = true;
                }

                var it = new RoutingItemModel()
                {
                    IsActive = def,
                    RuleNum = item.RuleNum,
                    Id = item.Id,
                    Remarks = item.Remarks,
                    Url = item.Url,
                    CustomIcon = item.CustomIcon,
                    CustomRulesetPath4Singbox = item.CustomRulesetPath4Singbox,
                    Sort = item.Sort,
                };
                _routingItems.Add(it);
            }
        }

        private async Task SaveRoutingAsync()
        {
            _config.RoutingBasicItem.DomainStrategy = domainStrategy;
            _config.RoutingBasicItem.EnableRoutingAdvanced = enableRoutingAdvanced;
            _config.RoutingBasicItem.DomainMatcher = domainMatcher;
            _config.RoutingBasicItem.DomainStrategy4Singbox = domainStrategy4Singbox;

            await EndBindingLockedData();

            if (await ConfigHandler.SaveConfig(_config) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }

        #endregion Refresh Save

        private async Task RoutingBasicImportRules()
        {
            //Extra to bypass the mainland
            ProxyDomain = "geosite:google";
            DirectDomain = "geosite:cn";
            DirectIP = "geoip:private,geoip:cn";
            BlockDomain = "geosite:category-ads-all";

            //NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
        }

        public async Task RoutingAdvancedEditAsync(bool blNew)
        {
            RoutingItem item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = await AppHandler.Instance.GetRoutingItem(SelectedSource?.Id);
                if (item is null)
                {
                    return;
                }
            }
            if (await _updateView?.Invoke(EViewAction.RoutingRuleSettingWindow, item) == true)
            {
                await RefreshRoutingItems();
                IsModified = true;
            }
        }

        public async Task RoutingAdvancedRemoveAsync()
        {
            if (SelectedSource is null || SelectedSource.Remarks.IsNullOrEmpty())
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectRules);
                return;
            }
            if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
            {
                return;
            }
            foreach (var it in SelectedSources ?? [SelectedSource])
            {
                var item = await AppHandler.Instance.GetRoutingItem(it?.Id);
                if (item != null)
                {
                    await ConfigHandler.RemoveRoutingItem(item);
                }
            }

            await RefreshRoutingItems();
            IsModified = true;
        }

        public async Task RoutingAdvancedSetDefault()
        {
            var item = await AppHandler.Instance.GetRoutingItem(SelectedSource?.Id);
            if (item is null)
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectRules);
                return;
            }

            if (await ConfigHandler.SetDefaultRouting(_config, item) == 0)
            {
                await RefreshRoutingItems();
                IsModified = true;
            }
        }

        private async Task RoutingAdvancedImportRules()
        {
            if (await ConfigHandler.InitRouting(_config, true) == 0)
            {
                await RefreshRoutingItems();
                IsModified = true;
            }
        }
    }
}