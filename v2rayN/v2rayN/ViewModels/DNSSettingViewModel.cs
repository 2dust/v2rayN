using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using v2rayN.Base;
using v2rayN.Enums;
using v2rayN.Handler;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class DNSSettingViewModel : MyReactiveObject
    {
        [Reactive] public bool useSystemHosts { get; set; }
        [Reactive] public string domainStrategy4Freedom { get; set; }
        [Reactive] public string domainDNSAddress { get; set; }
        [Reactive] public string normalDNS { get; set; }

        [Reactive] public string domainStrategy4Freedom2 { get; set; }
        [Reactive] public string domainDNSAddress2 { get; set; }
        [Reactive] public string normalDNS2 { get; set; }
        [Reactive] public string tunDNS2 { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportDefConfig4V2rayCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportDefConfig4SingboxCmd { get; }

        public DNSSettingViewModel(Func<EViewAction, object?, bool>? updateView)
        {
            _config = LazyConfig.Instance.Config;
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _updateView = updateView;

            var item = LazyConfig.Instance.GetDNSItem(ECoreType.Xray);
            useSystemHosts = item.useSystemHosts;
            domainStrategy4Freedom = item?.domainStrategy4Freedom ?? string.Empty;
            domainDNSAddress = item?.domainDNSAddress ?? string.Empty;
            normalDNS = item?.normalDNS ?? string.Empty;

            var item2 = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
            domainStrategy4Freedom2 = item2?.domainStrategy4Freedom ?? string.Empty;
            domainDNSAddress2 = item2?.domainDNSAddress ?? string.Empty;
            normalDNS2 = item2?.normalDNS ?? string.Empty;
            tunDNS2 = item2?.tunDNS ?? string.Empty;

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveSetting();
            });

            ImportDefConfig4V2rayCmd = ReactiveCommand.Create(() =>
            {
                normalDNS = Utils.GetEmbedText(Global.DNSV2rayNormalFileName);
            });

            ImportDefConfig4SingboxCmd = ReactiveCommand.Create(() =>
            {
                normalDNS2 = Utils.GetEmbedText(Global.DNSSingboxNormalFileName);
                tunDNS2 = Utils.GetEmbedText(Global.TunSingboxDNSFileName);
            });
        }

        private void SaveSetting()
        {
            if (!Utils.IsNullOrEmpty(normalDNS))
            {
                var obj = JsonUtils.ParseJson(normalDNS);
                if (obj != null && obj["servers"] != null)
                {
                }
                else
                {
                    if (normalDNS.Contains("{") || normalDNS.Contains("}"))
                    {
                        _noticeHandler?.Enqueue(ResUI.FillCorrectDNSText);
                        return;
                    }
                }
            }
            if (!Utils.IsNullOrEmpty(normalDNS2))
            {
                var obj2 = JsonUtils.Deserialize<Dns4Sbox>(normalDNS2);
                if (obj2 == null)
                {
                    _noticeHandler?.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }
            if (!Utils.IsNullOrEmpty(tunDNS2))
            {
                var obj2 = JsonUtils.Deserialize<Dns4Sbox>(tunDNS2);
                if (obj2 == null)
                {
                    _noticeHandler?.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }

            var item = LazyConfig.Instance.GetDNSItem(ECoreType.Xray);
            item.domainStrategy4Freedom = domainStrategy4Freedom;
            item.domainDNSAddress = domainDNSAddress;
            item.useSystemHosts = useSystemHosts;
            item.normalDNS = normalDNS;
            ConfigHandler.SaveDNSItems(_config, item);

            var item2 = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
            item2.domainStrategy4Freedom = domainStrategy4Freedom2;
            item2.domainDNSAddress = domainDNSAddress2;
            item2.normalDNS = JsonUtils.Serialize(JsonUtils.ParseJson(normalDNS2));
            item2.tunDNS = JsonUtils.Serialize(JsonUtils.ParseJson(tunDNS2)); ;
            ConfigHandler.SaveDNSItems(_config, item2);

            _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
    }
}