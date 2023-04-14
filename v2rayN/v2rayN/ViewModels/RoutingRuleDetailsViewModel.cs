using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Base;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class RoutingRuleDetailsViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;

        public IList<string> ProtocolItems { get; set; }
        public IList<string> InboundTagItems { get; set; }

        [Reactive]
        public RulesItem SelectedSource { get; set; }

        [Reactive]
        public string Domain { get; set; }

        [Reactive]
        public string IP { get; set; }

        [Reactive]
        public bool AutoSort { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public RoutingRuleDetailsViewModel(RulesItem rulesItem, Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;

            if (rulesItem.id.IsNullOrEmpty())
            {
                rulesItem.id = Utils.GetGUID(false);
                rulesItem.outboundTag = Global.agentTag;
                rulesItem.enabled = true;
                SelectedSource = rulesItem;
            }
            else
            {
                SelectedSource = rulesItem;
            }

            Domain = Utils.List2String(SelectedSource.domain, true);
            IP = Utils.List2String(SelectedSource.ip, true);

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveRules();
            });

            Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void SaveRules()
        {
            if (AutoSort)
            {
                SelectedSource.domain = Utils.String2ListSorted(Domain);
                SelectedSource.ip = Utils.String2ListSorted(IP);
            }
            else
            {
                SelectedSource.domain = Utils.String2List(Domain);
                SelectedSource.ip = Utils.String2List(IP);
            }
            SelectedSource.protocol = ProtocolItems?.ToList();
            SelectedSource.inboundTag = InboundTagItems?.ToList();

            bool hasRule =
              SelectedSource.domain != null
              && SelectedSource.domain.Count > 0
              || SelectedSource.ip != null
              && SelectedSource.ip.Count > 0
              || SelectedSource.protocol != null
              && SelectedSource.protocol.Count > 0
              || !Utils.IsNullOrEmpty(SelectedSource.port);

            if (!hasRule)
            {
                UI.ShowWarning(string.Format(ResUI.RoutingRuleDetailRequiredTips, "Port/Protocol/Domain/IP"));
                return;
            }
            //_noticeHandler?.Enqueue(ResUI.OperationSuccess);
            _view.DialogResult = true;
        }
    }
}