using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels
{
    public class DNSSettingViewModel : MyReactiveObject
    {
        [Reactive] public bool UseSystemHosts { get; set; }
        [Reactive] public string DomainStrategy4Freedom { get; set; }
        [Reactive] public string DomainDNSAddress { get; set; }
        [Reactive] public string NormalDNS { get; set; }

        [Reactive] public string DomainStrategy4Freedom2 { get; set; }
        [Reactive] public string DomainDNSAddress2 { get; set; }
        [Reactive] public string NormalDNS2 { get; set; }
        [Reactive] public string TunDNS2 { get; set; }

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
                NormalDNS = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
                await Task.CompletedTask;
            });

            ImportDefConfig4SingboxCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                NormalDNS2 = EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName);
                TunDNS2 = EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName);
                await Task.CompletedTask;
            });

            _ = Init();
        }

        private async Task Init()
        {
            var item = await AppHandler.Instance.GetDNSItem(ECoreType.Xray);
            UseSystemHosts = item.UseSystemHosts;
            DomainStrategy4Freedom = item?.DomainStrategy4Freedom ?? string.Empty;
            DomainDNSAddress = item?.DomainDNSAddress ?? string.Empty;
            NormalDNS = item?.NormalDNS ?? string.Empty;

            var item2 = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
            DomainStrategy4Freedom2 = item2?.DomainStrategy4Freedom ?? string.Empty;
            DomainDNSAddress2 = item2?.DomainDNSAddress ?? string.Empty;
            NormalDNS2 = item2?.NormalDNS ?? string.Empty;
            TunDNS2 = item2?.TunDNS ?? string.Empty;
        }

        private async Task SaveSettingAsync()
        {
            if (NormalDNS.IsNotEmpty())
            {
                var obj = JsonUtils.ParseJson(NormalDNS);
                if (obj != null && obj["servers"] != null)
                {
                }
                else
                {
                    if (NormalDNS.Contains('{') || NormalDNS.Contains('}'))
                    {
                        NoticeHandler.Instance.Enqueue(ResUI.FillCorrectDNSText);
                        return;
                    }
                }
            }
            if (NormalDNS2.IsNotEmpty())
            {
                var obj2 = JsonUtils.Deserialize<Dns4Sbox>(NormalDNS2);
                if (obj2 == null)
                {
                    NoticeHandler.Instance.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }
            if (TunDNS2.IsNotEmpty())
            {
                var obj2 = JsonUtils.Deserialize<Dns4Sbox>(TunDNS2);
                if (obj2 == null)
                {
                    NoticeHandler.Instance.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }

            var item = await AppHandler.Instance.GetDNSItem(ECoreType.Xray);
            item.DomainStrategy4Freedom = DomainStrategy4Freedom;
            item.DomainDNSAddress = DomainDNSAddress;
            item.UseSystemHosts = UseSystemHosts;
            item.NormalDNS = NormalDNS;
            await ConfigHandler.SaveDNSItems(_config, item);

            var item2 = await AppHandler.Instance.GetDNSItem(ECoreType.sing_box);
            item2.DomainStrategy4Freedom = DomainStrategy4Freedom2;
            item2.DomainDNSAddress = DomainDNSAddress2;
            item2.NormalDNS = JsonUtils.Serialize(JsonUtils.ParseJson(NormalDNS2));
            item2.TunDNS = JsonUtils.Serialize(JsonUtils.ParseJson(TunDNS2));
            await ConfigHandler.SaveDNSItems(_config, item2);

            NoticeHandler.Instance.Enqueue(ResUI.OperationSuccess);
            _ = _updateView?.Invoke(EViewAction.CloseWindow, null);
        }
    }
}
