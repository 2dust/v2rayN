using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace ServiceLib.ViewModels
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

        public DNSSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
        {
            _config = AppHandler.Instance.Config;

            _updateView = updateView;

            var item = AppHandler.Instance.GetDNSItem(ECoreType.Xray).Result;
            useSystemHosts = item.useSystemHosts;
            domainStrategy4Freedom = item?.domainStrategy4Freedom ?? string.Empty;
            domainDNSAddress = item?.domainDNSAddress ?? string.Empty;
            normalDNS = item?.normalDNS ?? string.Empty;

            var item2 = AppHandler.Instance.GetDNSItem(ECoreType.sing_box).Result;
            domainStrategy4Freedom2 = item2?.domainStrategy4Freedom ?? string.Empty;
            domainDNSAddress2 = item2?.domainDNSAddress ?? string.Empty;
            normalDNS2 = item2?.normalDNS ?? string.Empty;
            tunDNS2 = item2?.tunDNS ?? string.Empty;

            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveSettingAsync();
            });

            ImportDefConfig4V2rayCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                normalDNS = Utils.GetEmbedText(Global.DNSV2rayNormalFileName);
            });

            ImportDefConfig4SingboxCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                normalDNS2 = Utils.GetEmbedText(Global.DNSSingboxNormalFileName);
                tunDNS2 = Utils.GetEmbedText(Global.TunSingboxDNSFileName);
            });
        }

        private async Task SaveSettingAsync()
        {
            if (Utils.IsNotEmpty(normalDNS))
            {
                var obj = JsonUtils.ParseJson(normalDNS);
                if (obj != null && obj["servers"] != null)
                {
                }
                else
                {
                    if (normalDNS.Contains('{') || normalDNS.Contains('}'))
                    {
                        NoticeHandler.Instance.Enqueue(ResUI.FillCorrectDNSText);
                        return;
                    }
                }
            }
            if (Utils.IsNotEmpty(normalDNS2))
            {
                var obj2 = JsonUtils.Deserialize<Dns4Sbox>(normalDNS2);
                if (obj2 == null)
                {
                    NoticeHandler.Instance.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }
            if (Utils.IsNotEmpty(tunDNS2))
            {
                var obj2 = JsonUtils.Deserialize<Dns4Sbox>(tunDNS2);
                if (obj2 == null)
                {
                    NoticeHandler.Instance.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }

            var item = await AppHandler.Instance.GetDNSItem(ECoreType.Xray);
            item.domainStrategy4Freedom = domainStrategy4Freedom;
            item.domainDNSAddress = domainDNSAddress;
            item.useSystemHosts = useSystemHosts;
            item.normalDNS = normalDNS;
            await ConfigHandler.SaveDNSItems(_config, item);

            var item2 = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
            item2.domainStrategy4Freedom = domainStrategy4Freedom2;
            item2.domainDNSAddress = domainDNSAddress2;
            item2.normalDNS = JsonUtils.Serialize(JsonUtils.ParseJson(normalDNS2));
            item2.tunDNS = JsonUtils.Serialize(JsonUtils.ParseJson(tunDNS2)); ;
            await ConfigHandler.SaveDNSItems(_config, item2);

            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
    }
}