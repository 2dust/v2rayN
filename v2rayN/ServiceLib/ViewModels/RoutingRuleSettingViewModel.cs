using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ServiceLib.Services;
using Splat;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class RoutingRuleSettingViewModel : MyReactiveObject
    {
        private List<RulesItem> _rules;

        [Reactive]
        public RoutingItem SelectedRouting { get; set; }

        private IObservableCollection<RulesItemModel> _rulesItems = new ObservableCollectionExtended<RulesItemModel>();
        public IObservableCollection<RulesItemModel> RulesItems => _rulesItems;

        [Reactive]
        public RulesItemModel SelectedSource { get; set; }

        public IList<RulesItemModel> SelectedSources { get; set; }

        public ReactiveCommand<Unit, Unit> RuleAddCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportRulesFromFileCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportRulesFromClipboardCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportRulesFromUrlCmd { get; }
        public ReactiveCommand<Unit, Unit> RuleRemoveCmd { get; }
        public ReactiveCommand<Unit, Unit> RuleExportSelectedCmd { get; }
        public ReactiveCommand<Unit, Unit> MoveTopCmd { get; }
        public ReactiveCommand<Unit, Unit> MoveUpCmd { get; }
        public ReactiveCommand<Unit, Unit> MoveDownCmd { get; }
        public ReactiveCommand<Unit, Unit> MoveBottomCmd { get; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public RoutingRuleSettingViewModel(RoutingItem routingItem, Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _updateView = updateView;
            SelectedSource = new();

            if (routingItem.id.IsNullOrEmpty())
            {
                SelectedRouting = routingItem;
                _rules = new();
            }
            else
            {
                SelectedRouting = routingItem;
                _rules = JsonUtils.Deserialize<List<RulesItem>>(SelectedRouting.ruleSet);
            }

            RefreshRulesItems();

            var canEditRemove = this.WhenAnyValue(
               x => x.SelectedSource,
               selectedSource => selectedSource != null && !selectedSource.outboundTag.IsNullOrEmpty());

            RuleAddCmd = ReactiveCommand.Create(() =>
            {
                RuleEditAsync(true);
            });
            ImportRulesFromFileCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await _updateView?.Invoke(EViewAction.ImportRulesFromFile, null);
            });
            ImportRulesFromClipboardCmd = ReactiveCommand.Create(() =>
            {
                ImportRulesFromClipboardAsync(null);
            });
            ImportRulesFromUrlCmd = ReactiveCommand.Create(() =>
            {
                ImportRulesFromUrl();
            });

            RuleRemoveCmd = ReactiveCommand.Create(() =>
            {
                RuleRemoveAsync();
            }, canEditRemove);
            RuleExportSelectedCmd = ReactiveCommand.Create(() =>
            {
                RuleExportSelectedAsync();
            }, canEditRemove);

            MoveTopCmd = ReactiveCommand.Create(() =>
            {
                MoveRule(EMove.Top);
            }, canEditRemove);
            MoveUpCmd = ReactiveCommand.Create(() =>
            {
                MoveRule(EMove.Up);
            }, canEditRemove);
            MoveDownCmd = ReactiveCommand.Create(() =>
            {
                MoveRule(EMove.Down);
            }, canEditRemove);
            MoveBottomCmd = ReactiveCommand.Create(() =>
            {
                MoveRule(EMove.Bottom);
            }, canEditRemove);

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveRoutingAsync();
            });
        }

        public void RefreshRulesItems()
        {
            _rulesItems.Clear();

            foreach (var item in _rules)
            {
                var it = new RulesItemModel()
                {
                    id = item.id,
                    outboundTag = item.outboundTag,
                    port = item.port,
                    network = item.network,
                    protocols = Utils.List2String(item.protocol),
                    inboundTags = Utils.List2String(item.inboundTag),
                    domains = Utils.List2String(item.domain),
                    ips = Utils.List2String(item.ip),
                    enabled = item.enabled,
                    remarks = item.remarks,
                };
                _rulesItems.Add(it);
            }
        }

        public async Task RuleEditAsync(bool blNew)
        {
            RulesItem? item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = _rules.FirstOrDefault(t => t.id == SelectedSource?.id);
                if (item is null)
                {
                    return;
                }
            }
            if (await _updateView?.Invoke(EViewAction.RoutingRuleDetailsWindow, item) == true)
            {
                if (blNew)
                {
                    _rules.Add(item);
                }
                RefreshRulesItems();
            }
        }

        public async Task RuleRemoveAsync()
        {
            if (SelectedSource is null || SelectedSource.outboundTag.IsNullOrEmpty())
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectRules);
                return;
            }
            if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
            {
                return;
            }
            foreach (var it in SelectedSources ?? [SelectedSource])
            {
                var item = _rules.FirstOrDefault(t => t.id == it?.id);
                if (item != null)
                {
                    _rules.Remove(item);
                }
            }

            RefreshRulesItems();
        }

        public async Task RuleExportSelectedAsync()
        {
            if (SelectedSource is null || SelectedSource.outboundTag.IsNullOrEmpty())
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectRules);
                return;
            }

            var lst = new List<RulesItem4Ray>();
            foreach (var it in SelectedSources ?? [SelectedSource])
            {
                var item = _rules.FirstOrDefault(t => t.id == it?.id);
                if (item != null)
                {
                    var item2 = JsonUtils.Deserialize<RulesItem4Ray>(JsonUtils.Serialize(item));
                    lst.Add(item2 ?? new());
                }
            }
            if (lst.Count > 0)
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, JsonUtils.Serialize(lst));
            }
        }

        public void MoveRule(EMove eMove)
        {
            if (SelectedSource is null || SelectedSource.outboundTag.IsNullOrEmpty())
            {
                _noticeHandler?.Enqueue(ResUI.PleaseSelectRules);
                return;
            }

            var item = _rules.FirstOrDefault(t => t.id == SelectedSource?.id);
            if (item == null)
            {
                return;
            }
            var index = _rules.IndexOf(item);
            if (ConfigHandler.MoveRoutingRule(_rules, index, eMove) == 0)
            {
                RefreshRulesItems();
            }
        }

        private async Task SaveRoutingAsync()
        {
            string remarks = SelectedRouting.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                _noticeHandler?.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }
            var item = SelectedRouting;
            foreach (var it in _rules)
            {
                it.id = Utils.GetGUID(false);
            }
            item.ruleNum = _rules.Count;
            item.ruleSet = JsonUtils.Serialize(_rules, false);

            if (ConfigHandler.SaveRoutingItem(_config, item) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                await _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                _noticeHandler?.Enqueue(ResUI.OperationFailed);
            }
        }

        #region Import rules

        public async Task ImportRulesFromFileAsync(string fileName)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            string result = Utils.LoadResource(fileName);
            if (Utils.IsNullOrEmpty(result))
            {
                return;
            }
            var ret = await AddBatchRoutingRulesAsync(SelectedRouting, result);
            if (ret == 0)
            {
                RefreshRulesItems();
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            }
        }

        public async Task ImportRulesFromClipboardAsync(string? clipboardData)
        {
            if (clipboardData == null)
            {
                await _updateView?.Invoke(EViewAction.ImportRulesFromClipboard, null);
                return;
            }
            var ret = await AddBatchRoutingRulesAsync(SelectedRouting, clipboardData);
            if (ret == 0)
            {
                RefreshRulesItems();
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            }
        }

        private async void ImportRulesFromUrl()
        {
            var url = SelectedRouting.url;
            if (Utils.IsNullOrEmpty(url))
            {
                _noticeHandler?.Enqueue(ResUI.MsgNeedUrl);
                return;
            }

            DownloadService downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, true, "");
            var ret = await AddBatchRoutingRulesAsync(SelectedRouting, result);
            if (ret == 0)
            {
                RefreshRulesItems();
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            }
        }

        private async Task<int> AddBatchRoutingRulesAsync(RoutingItem routingItem, string? clipboardData)
        {
            bool blReplace = false;
            if (await _updateView?.Invoke(EViewAction.AddBatchRoutingRulesYesNo, null) == false)
            {
                blReplace = true;
            }
            if (Utils.IsNullOrEmpty(clipboardData))
            {
                return -1;
            }
            var lstRules = JsonUtils.Deserialize<List<RulesItem>>(clipboardData);
            if (lstRules == null)
            {
                return -1;
            }
            foreach (var rule in lstRules)
            {
                rule.id = Utils.GetGUID(false);
            }

            if (blReplace)
            {
                _rules = lstRules;
            }
            else
            {
                _rules.AddRange(lstRules);
            }
            return 0;
        }

        #endregion Import rules
    }
}