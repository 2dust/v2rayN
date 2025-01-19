using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            _updateView = updateView;

            var canEditRemove = this.WhenAnyValue(
                x => x.SelectedSource,
                selectedSource => selectedSource != null && !selectedSource.OutboundTag.IsNullOrEmpty());

            RuleAddCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RuleEditAsync(true);
            });
            ImportRulesFromFileCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await _updateView?.Invoke(EViewAction.ImportRulesFromFile, null);
            });
            ImportRulesFromClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ImportRulesFromClipboardAsync(null);
            });
            ImportRulesFromUrlCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await ImportRulesFromUrl();
            });

            RuleRemoveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RuleRemoveAsync();
            }, canEditRemove);
            RuleExportSelectedCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await RuleExportSelectedAsync();
            }, canEditRemove);

            MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveRule(EMove.Top);
            }, canEditRemove);
            MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveRule(EMove.Up);
            }, canEditRemove);
            MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveRule(EMove.Down);
            }, canEditRemove);
            MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await MoveRule(EMove.Bottom);
            }, canEditRemove);

            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveRoutingAsync();
            });

            SelectedSource = new();
            SelectedRouting = routingItem;
            _rules = routingItem.Id.IsNullOrEmpty() ? new() : JsonUtils.Deserialize<List<RulesItem>>(SelectedRouting.RuleSet);

            RefreshRulesItems();
        }

        public void RefreshRulesItems()
        {
            _rulesItems.Clear();

            foreach (var item in _rules)
            {
                var it = new RulesItemModel()
                {
                    Id = item.Id,
                    OutboundTag = item.OutboundTag,
                    Port = item.Port,
                    Network = item.Network,
                    Protocols = Utils.List2String(item.Protocol),
                    InboundTags = Utils.List2String(item.InboundTag),
                    Domains = Utils.List2String(item.Domain),
                    Ips = Utils.List2String(item.Ip),
                    Enabled = item.Enabled,
                    Remarks = item.Remarks,
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
                item = _rules.FirstOrDefault(t => t.Id == SelectedSource?.Id);
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
            if (SelectedSource is null || SelectedSource.OutboundTag.IsNullOrEmpty())
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
                var item = _rules.FirstOrDefault(t => t.Id == it?.Id);
                if (item != null)
                {
                    _rules.Remove(item);
                }
            }

            RefreshRulesItems();
        }

        public async Task RuleExportSelectedAsync()
        {
            if (SelectedSource is null || SelectedSource.OutboundTag.IsNullOrEmpty())
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectRules);
                return;
            }

            var lst = new List<RulesItem>();
            var sources = SelectedSources ?? [SelectedSource];
            foreach (var it in _rules)
            {
                if (sources.Any(t => t.Id == it?.Id))
                {
                    var item2 = JsonUtils.DeepCopy(it);
                    item2.Id = null;
                    lst.Add(item2 ?? new());
                }
            }
            if (lst.Count > 0)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                await _updateView?.Invoke(EViewAction.SetClipboardData, JsonUtils.Serialize(lst, options));
            }
        }

        public async Task MoveRule(EMove eMove)
        {
            if (SelectedSource is null || SelectedSource.OutboundTag.IsNullOrEmpty())
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseSelectRules);
                return;
            }

            var item = _rules.FirstOrDefault(t => t.Id == SelectedSource?.Id);
            if (item == null)
            {
                return;
            }
            var index = _rules.IndexOf(item);
            if (await ConfigHandler.MoveRoutingRule(_rules, index, eMove) == 0)
            {
                RefreshRulesItems();
            }
        }

        private async Task SaveRoutingAsync()
        {
            string remarks = SelectedRouting.Remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                NoticeHandler.Instance.Enqueue(ResUI.PleaseFillRemarks);
                return;
            }
            var item = SelectedRouting;
            foreach (var it in _rules)
            {
                it.Id = Utils.GetGuid(false);
            }
            item.RuleNum = _rules.Count;
            item.RuleSet = JsonUtils.Serialize(_rules, false);

            if (await ConfigHandler.SaveRoutingItem(_config, item) == 0)
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
                _updateView?.Invoke(EViewAction.CloseWindow, null);
            }
            else
            {
                NoticeHandler.Instance.Enqueue(ResUI.OperationFailed);
            }
        }

        #region Import rules

        public async Task ImportRulesFromFileAsync(string fileName)
        {
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            var result = Utils.LoadResource(fileName);
            if (Utils.IsNullOrEmpty(result))
            {
                return;
            }
            var ret = await AddBatchRoutingRulesAsync(SelectedRouting, result);
            if (ret == 0)
            {
                RefreshRulesItems();
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
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
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            }
        }

        private async Task ImportRulesFromUrl()
        {
            var url = SelectedRouting.Url;
            if (Utils.IsNullOrEmpty(url))
            {
                NoticeHandler.Instance.Enqueue(ResUI.MsgNeedUrl);
                return;
            }

            DownloadService downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, true, "");
            var ret = await AddBatchRoutingRulesAsync(SelectedRouting, result);
            if (ret == 0)
            {
                RefreshRulesItems();
                NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
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
                rule.Id = Utils.GetGuid(false);
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