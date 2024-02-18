﻿using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Model;
using v2rayN.Resx;
using v2rayN.Views;

namespace v2rayN.ViewModels
{
    public class RoutingSettingViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;
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

        public RoutingSettingViewModel(Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;
            SelectedSource = new();

            ConfigHandler.InitBuiltinRouting(_config);

            enableRoutingAdvanced = _config.routingBasicItem.enableRoutingAdvanced;
            domainStrategy = _config.routingBasicItem.domainStrategy;
            domainMatcher = _config.routingBasicItem.domainMatcher;
            domainStrategy4Singbox = _config.routingBasicItem.domainStrategy4Singbox;

            RefreshRoutingItems();

            BindingLockedData();

            var canEditRemove = this.WhenAnyValue(
               x => x.SelectedSource,
               selectedSource => selectedSource != null && !selectedSource.remarks.IsNullOrEmpty());

            this.WhenAnyValue(
                x => x.enableRoutingAdvanced)
                .Subscribe(c => enableRoutingBasic = !enableRoutingAdvanced);

            RoutingBasicImportRulesCmd = ReactiveCommand.Create(() =>
            {
                RoutingBasicImportRules();
            });

            RoutingAdvancedAddCmd = ReactiveCommand.Create(() =>
            {
                RoutingAdvancedEdit(true);
            });
            RoutingAdvancedRemoveCmd = ReactiveCommand.Create(() =>
            {
                RoutingAdvancedRemove();
            }, canEditRemove);
            RoutingAdvancedSetDefaultCmd = ReactiveCommand.Create(() =>
            {
                RoutingAdvancedSetDefault();
            }, canEditRemove);
            RoutingAdvancedImportRulesCmd = ReactiveCommand.Create(() =>
            {
                RoutingAdvancedImportRules();
            });

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveRouting();
            });

            Utile.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        #region locked

        private void BindingLockedData()
        {
            _lockedItem = ConfigHandler.GetLockedRoutingItem(_config);
            if (_lockedItem != null)
            {
                _lockedRules = JsonUtile.Deserialize<List<RulesItem>>(_lockedItem.ruleSet);
                ProxyDomain = Utile.List2String(_lockedRules[0].domain, true);
                ProxyIP = Utile.List2String(_lockedRules[0].ip, true);

                DirectDomain = Utile.List2String(_lockedRules[1].domain, true);
                DirectIP = Utile.List2String(_lockedRules[1].ip, true);

                BlockDomain = Utile.List2String(_lockedRules[2].domain, true);
                BlockIP = Utile.List2String(_lockedRules[2].ip, true);
            }
        }

        private void EndBindingLockedData()
        {
            if (_lockedItem != null)
            {
                _lockedRules[0].domain = Utile.String2List(Utile.Convert2Comma(ProxyDomain.TrimEx()));
                _lockedRules[0].ip = Utile.String2List(Utile.Convert2Comma(ProxyIP.TrimEx()));

                _lockedRules[1].domain = Utile.String2List(Utile.Convert2Comma(DirectDomain.TrimEx()));
                _lockedRules[1].ip = Utile.String2List(Utile.Convert2Comma(DirectIP.TrimEx()));

                _lockedRules[2].domain = Utile.String2List(Utile.Convert2Comma(BlockDomain.TrimEx()));
                _lockedRules[2].ip = Utile.String2List(Utile.Convert2Comma(BlockIP.TrimEx()));

                _lockedItem.ruleSet = JsonUtile.Serialize(_lockedRules, false);

                ConfigHandler.SaveRoutingItem(_config, _lockedItem);
            }
        }

        #endregion locked

        #region Refresh Save

        public void RefreshRoutingItems()
        {
            _routingItems.Clear();

            var routings = LazyConfig.Instance.RoutingItems();
            foreach (var item in routings)
            {
                bool def = false;
                if (item.id == _config.routingBasicItem.routingIndexId)
                {
                    def = true;
                }

                var it = new RoutingItemModel()
                {
                    isActive = def,
                    ruleNum = item.ruleNum,
                    id = item.id,
                    remarks = item.remarks,
                    url = item.url,
                    customIcon = item.customIcon,
                    sort = item.sort,
                };
                _routingItems.Add(it);
            }
        }

        private void SaveRouting()
        {
            _config.routingBasicItem.domainStrategy = domainStrategy;
            _config.routingBasicItem.enableRoutingAdvanced = enableRoutingAdvanced;
            _config.routingBasicItem.domainMatcher = domainMatcher;
            _config.routingBasicItem.domainStrategy4Singbox = domainStrategy4Singbox;

            EndBindingLockedData();

            if (ConfigHandler.SaveConfig(_config) == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                _view.DialogResult = true;
            }
            else
            {
                UI.ShowWarning(ResUI.OperationFailed);
            }
        }

        #endregion Refresh Save

        private void RoutingBasicImportRules()
        {
            //Extra to bypass the mainland
            ProxyDomain = "geosite:google";
            DirectDomain = "geosite:cn";
            DirectIP = "geoip:private,geoip:cn";
            BlockDomain = "geosite:category-ads-all";

            //_noticeHandler?.Enqueue(ResUI.OperationSuccess);
            UI.Show(ResUI.OperationSuccess);
        }

        public void RoutingAdvancedEdit(bool blNew)
        {
            RoutingItem item;
            if (blNew)
            {
                item = new();
            }
            else
            {
                item = LazyConfig.Instance.GetRoutingItem(SelectedSource?.id);
                if (item is null)
                {
                    return;
                }
            }
            var ret = (new RoutingRuleSettingWindow(item)).ShowDialog();
            if (ret == true)
            {
                RefreshRoutingItems();
                IsModified = true;
            }
        }

        public void RoutingAdvancedRemove()
        {
            if (SelectedSource is null || SelectedSource.remarks.IsNullOrEmpty())
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
                var item = LazyConfig.Instance.GetRoutingItem(it?.id);
                if (item != null)
                {
                    ConfigHandler.RemoveRoutingItem(item);
                }
            }

            RefreshRoutingItems();
            IsModified = true;
        }

        public void RoutingAdvancedSetDefault()
        {
            var item = LazyConfig.Instance.GetRoutingItem(SelectedSource?.id);
            if (item is null)
            {
                UI.Show(ResUI.PleaseSelectRules);
                return;
            }

            if (ConfigHandler.SetDefaultRouting(_config, item) == 0)
            {
                RefreshRoutingItems();
                IsModified = true;
            }
        }

        private void RoutingAdvancedImportRules()
        {
            if (ConfigHandler.InitBuiltinRouting(_config, true) == 0)
            {
                RefreshRoutingItems();
                IsModified = true;
            }
        }
    }
}