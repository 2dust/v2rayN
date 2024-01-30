using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;
using v2rayN.Views;
using Application = System.Windows.Application;

namespace v2rayN.ViewModels
{
    public class RoutingRuleSettingViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;
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

        public RoutingRuleSettingViewModel(RoutingItem routingItem, Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;
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
                RuleEdit(true);
            });
            ImportRulesFromFileCmd = ReactiveCommand.Create(() =>
            {
                ImportRulesFromFile();
            });
            ImportRulesFromClipboardCmd = ReactiveCommand.Create(() =>
            {
                ImportRulesFromClipboard();
            });
            ImportRulesFromUrlCmd = ReactiveCommand.CreateFromTask(() =>
            {
                return ImportRulesFromUrl();
            });

            RuleRemoveCmd = ReactiveCommand.Create(() =>
            {
                RuleRemove();
            }, canEditRemove);
            RuleExportSelectedCmd = ReactiveCommand.Create(() =>
            {
                RuleExportSelected();
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
                SaveRouting();
            });

            Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
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
                    protocols = Utils.List2String(item.protocol),
                    inboundTags = Utils.List2String(item.inboundTag),
                    domains = Utils.List2String(item.domain),
                    ips = Utils.List2String(item.ip),
                    enabled = item.enabled,
                };
                _rulesItems.Add(it);
            }
        }

        public void RuleEdit(bool blNew)
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
            var ret = (new RoutingRuleDetailsWindow(item)).ShowDialog();
            if (ret == true)
            {
                if (blNew)
                {
                    _rules.Add(item);
                }
                RefreshRulesItems();
            }
        }

        public void RuleRemove()
        {
            if (SelectedSource is null || SelectedSource.outboundTag.IsNullOrEmpty())
            {
                UI.Show(ResUI.PleaseSelectRules);
                return;
            }
            if (UI.ShowYesNo(ResUI.RemoveRules) == MessageBoxResult.No)
            {
                return;
            }
            foreach (var it in SelectedSources)
            {
                var item = _rules.FirstOrDefault(t => t.id == it?.id);
                if (item != null)
                {
                    _rules.Remove(item);
                }
            }

            RefreshRulesItems();
        }

        public void RuleExportSelected()
        {
            if (SelectedSource is null || SelectedSource.outboundTag.IsNullOrEmpty())
            {
                UI.Show(ResUI.PleaseSelectRules);
                return;
            }

            var lst = new List<RulesItem>();
            foreach (var it in SelectedSources)
            {
                var item = _rules.FirstOrDefault(t => t.id == it?.id);
                if (item != null)
                {
                    lst.Add(item);
                }
            }
            if (lst.Count > 0)
            {
                Utils.SetClipboardData(JsonUtils.Serialize(lst));
                //UI.Show(ResUI.OperationSuccess"));
            }
        }

        public void MoveRule(EMove eMove)
        {
            if (SelectedSource is null || SelectedSource.outboundTag.IsNullOrEmpty())
            {
                UI.Show(ResUI.PleaseSelectRules);
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

        private void SaveRouting()
        {
            string remarks = SelectedRouting.remarks;
            if (Utils.IsNullOrEmpty(remarks))
            {
                UI.Show(ResUI.PleaseFillRemarks);
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
                _view.DialogResult = true;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        #region Import rules

        private void ImportRulesFromFile()
        {
            if (UI.OpenFileDialog(out string fileName,
                "Rules|*.json|All|*.*") != true)
            {
                return;
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                return;
            }

            string result = Utils.LoadResource(fileName);
            if (Utils.IsNullOrEmpty(result))
            {
                return;
            }

            if (AddBatchRoutingRules(SelectedRouting, result) == 0)
            {
                RefreshRulesItems();
                UI.Show(ResUI.OperationSuccess);
            }
        }

        private void ImportRulesFromClipboard()
        {
            string clipboardData = Utils.GetClipboardData();
            if (AddBatchRoutingRules(SelectedRouting, clipboardData) == 0)
            {
                RefreshRulesItems();
                UI.Show(ResUI.OperationSuccess);
            }
        }

        private async Task ImportRulesFromUrl()
        {
            var url = SelectedRouting.url;
            if (Utils.IsNullOrEmpty(url))
            {
                UI.Show(ResUI.MsgNeedUrl);
                return;
            }

            DownloadHandle downloadHandle = new DownloadHandle();
            var result = await downloadHandle.TryDownloadString(url, true, "");
            if (AddBatchRoutingRules(SelectedRouting, result) == 0)
            {
                Application.Current.Dispatcher.Invoke((Action)(() =>
                {
                    RefreshRulesItems();
                }));
                UI.Show(ResUI.OperationSuccess);
            }
        }

        private int AddBatchRoutingRules(RoutingItem routingItem, string? clipboardData)
        {
            bool blReplace = false;
            if (UI.ShowYesNo(ResUI.AddBatchRoutingRulesYesNo) == MessageBoxResult.No)
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