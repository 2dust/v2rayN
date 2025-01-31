using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

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
            SaveCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveSettingAsync();
            });

            ImportDefConfig4V2rayCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                normalDNS = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
                await Task.CompletedTask;
            });

            ImportDefConfig4SingboxCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                normalDNS2 = EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName);
                tunDNS2 = EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName);
                await Task.CompletedTask;
            });

            _ = Init();
        }

        private async Task Init()
        {
            var item = await AppHandler.Instance.GetDNSItem(ECoreType.Xray);
            useSystemHosts = item.UseSystemHosts;
            domainStrategy4Freedom = item?.DomainStrategy4Freedom ?? string.Empty;
            domainDNSAddress = item?.DomainDNSAddress ?? string.Empty;
            normalDNS = item?.NormalDNS ?? string.Empty;

            var item2 = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
            domainStrategy4Freedom2 = item2?.DomainStrategy4Freedom ?? string.Empty;
            domainDNSAddress2 = item2?.DomainDNSAddress ?? string.Empty;
            normalDNS2 = item2?.NormalDNS ?? string.Empty;
            tunDNS2 = item2?.TunDNS ?? string.Empty;
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
            item.DomainStrategy4Freedom = domainStrategy4Freedom;
            item.DomainDNSAddress = domainDNSAddress;
            item.UseSystemHosts = useSystemHosts;
            item.NormalDNS = normalDNS;
            await ConfigHandler.SaveDNSItems(_config, item);

            var item2 = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
            item2.DomainStrategy4Freedom = domainStrategy4Freedom2;
            item2.DomainDNSAddress = domainDNSAddress2;
            item2.NormalDNS = JsonUtils.Serialize(JsonUtils.ParseJson(normalDNS2));
            item2.TunDNS = JsonUtils.Serialize(JsonUtils.ParseJson(tunDNS2));
            await ConfigHandler.SaveDNSItems(_config, item2);

            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
    }
}
