using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Model;
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
        public string Process { get; set; }

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
                rulesItem.id = Utile.GetGUID(false);
                rulesItem.outboundTag = Global.ProxyTag;
                rulesItem.enabled = true;
                SelectedSource = rulesItem;
            }
            else
            {
                SelectedSource = rulesItem;
            }

            Domain = Utile.List2String(SelectedSource.domain, true);
            IP = Utile.List2String(SelectedSource.ip, true);
            Process = Utile.List2String(SelectedSource.process, true);

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveRules();
            });

            Utile.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void SaveRules()
        {
            Domain = Utile.Convert2Comma(Domain);
            IP = Utile.Convert2Comma(IP);
            Process = Utile.Convert2Comma(Process);

            if (AutoSort)
            {
                SelectedSource.domain = Utile.String2ListSorted(Domain);
                SelectedSource.ip = Utile.String2ListSorted(IP);
                SelectedSource.process = Utile.String2ListSorted(Process);
            }
            else
            {
                SelectedSource.domain = Utile.String2List(Domain);
                SelectedSource.ip = Utile.String2List(IP);
                SelectedSource.process = Utile.String2List(Process);
            }
            SelectedSource.protocol = ProtocolItems?.ToList();
            SelectedSource.inboundTag = InboundTagItems?.ToList();

            bool hasRule = SelectedSource.domain?.Count > 0
              || SelectedSource.ip?.Count > 0
              || SelectedSource.protocol?.Count > 0
              || SelectedSource.process?.Count > 0
              || !Utile.IsNullOrEmpty(SelectedSource.port);

            if (!hasRule)
            {
                _noticeHandler?.Enqueue(string.Format(ResUI.RoutingRuleDetailRequiredTips, "Port/Protocol/Domain/IP/Process"));
                return;
            }
            //_noticeHandler?.Enqueue(ResUI.OperationSuccess);
            _view.DialogResult = true;
        }
    }
}