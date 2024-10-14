﻿using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
{
    public class RoutingRuleDetailsViewModel : MyReactiveObject
    {
        public IList<string> ProtocolItems { get; set; }
        public IList<string> InboundTagItems { get; set; }

        [Reactive]
        public RulesItem SelectedSource { get; set; }

        [Reactive]
        public string Domain { get; set; }

        [Reactive]
        public string IP { get; set; }

        [Reactive]
        public string Process { get; set; }

        [Reactive]
        public bool AutoSort { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public RoutingRuleDetailsViewModel(RulesItem rulesItem, Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;

            _updateView = updateView;

            if (rulesItem.id.IsNullOrEmpty())
            {
                rulesItem.id = Utils.GetGuid(false);
                rulesItem.outboundTag = Global.ProxyTag;
                rulesItem.enabled = true;
                SelectedSource = rulesItem;
            }
            else
            {
                SelectedSource = rulesItem;
            }

            Domain = Utils.List2String(SelectedSource.domain, true);
            IP = Utils.List2String(SelectedSource.ip, true);
            Process = Utils.List2String(SelectedSource.process, true);

            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveRulesAsync();
            });
        }

        private async Task SaveRulesAsync()
        {
            Domain = Utils.Convert2Comma(Domain);
            IP = Utils.Convert2Comma(IP);
            Process = Utils.Convert2Comma(Process);

            if (AutoSort)
            {
                SelectedSource.domain = Utils.String2ListSorted(Domain);
                SelectedSource.ip = Utils.String2ListSorted(IP);
                SelectedSource.process = Utils.String2ListSorted(Process);
            }
            else
            {
                SelectedSource.domain = Utils.String2List(Domain);
                SelectedSource.ip = Utils.String2List(IP);
                SelectedSource.process = Utils.String2List(Process);
            }
            SelectedSource.protocol = ProtocolItems?.ToList();
            SelectedSource.inboundTag = InboundTagItems?.ToList();

            bool hasRule = SelectedSource.domain?.Count > 0
              || SelectedSource.ip?.Count > 0
              || SelectedSource.protocol?.Count > 0
              || SelectedSource.process?.Count > 0
              || Utils.IsNotEmpty(SelectedSource.port);

            if (!hasRule)
            {
                NoticeHandler.Instance.Enqueue(string.Format(ResUI.RoutingRuleDetailRequiredTips, "Port/Protocol/Domain/IP/Process"));
                return;
            }
            //NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
    }
}