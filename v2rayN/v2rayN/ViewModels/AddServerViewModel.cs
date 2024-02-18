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
    public class AddServerViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;

        [Reactive]
        public ProfileItem SelectedSource { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }

        public AddServerViewModel(ProfileItem profileItem, Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;

            if (profileItem.id.IsNullOrEmpty())
            {
                profileItem.network = Global.DefaultNetwork;
                profileItem.headerType = Global.None;
                profileItem.requestHost = "";
                profileItem.streamSecurity = "";
                SelectedSource = profileItem;
            }
            else
            {
                SelectedSource = JsonUtile.DeepCopy(profileItem);
            }

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveServer();
            });

            Utile.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void SaveServer()
        {
            if (Utile.IsNullOrEmpty(SelectedSource.remarks))
            {
                UI.Show(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utile.IsNullOrEmpty(SelectedSource.address))
            {
                UI.Show(ResUI.FillServerAddress);
                return;
            }
            var port = SelectedSource.port.ToString();
            if (Utile.IsNullOrEmpty(port) || !Utile.IsNumeric(port)
                || SelectedSource.port <= 0 || SelectedSource.port >= Global.MaxPort)
            {
                UI.Show(ResUI.FillCorrectServerPort);
                return;
            }
            if (SelectedSource.configType == EConfigType.Shadowsocks)
            {
                if (Utile.IsNullOrEmpty(SelectedSource.id))
                {
                    UI.Show(ResUI.FillPassword);
                    return;
                }
                if (Utile.IsNullOrEmpty(SelectedSource.security))
                {
                    UI.Show(ResUI.PleaseSelectEncryption);
                    return;
                }
            }
            if (SelectedSource.configType != EConfigType.Socks)
            {
                if (Utile.IsNullOrEmpty(SelectedSource.id))
                {
                    UI.Show(ResUI.FillUUID);
                    return;
                }
            }

            var item = LazyConfig.Instance.GetProfileItem(SelectedSource.indexId);
            if (item is null)
            {
                item = SelectedSource;
            }
            else
            {
                item.coreType = SelectedSource.coreType;
                item.remarks = SelectedSource.remarks;
                item.address = SelectedSource.address;
                item.port = SelectedSource.port;

                item.id = SelectedSource.id;
                item.alterId = SelectedSource.alterId;
                item.security = SelectedSource.security;
                item.flow = SelectedSource.flow;

                item.network = SelectedSource.network;
                item.headerType = SelectedSource.headerType;
                item.requestHost = SelectedSource.requestHost;
                item.path = SelectedSource.path;

                item.streamSecurity = SelectedSource.streamSecurity;
                item.sni = SelectedSource.sni;
                item.allowInsecure = SelectedSource.allowInsecure;
                item.fingerprint = SelectedSource.fingerprint;
                item.alpn = SelectedSource.alpn;

                item.publicKey = SelectedSource.publicKey;
                item.shortId = SelectedSource.shortId;
                item.spiderX = SelectedSource.spiderX;
            }

            var ret = item.configType switch
            {
                EConfigType.VMess => ConfigHandler.AddServer(_config, item),
                EConfigType.Shadowsocks => ConfigHandler.AddShadowsocksServer(_config, item),
                EConfigType.Socks => ConfigHandler.AddSocksServer(_config, item),
                EConfigType.Trojan => ConfigHandler.AddTrojanServer(_config, item),
                EConfigType.VLESS => ConfigHandler.AddVlessServer(_config, item),
                EConfigType.Hysteria2 => ConfigHandler.AddHysteria2Server(_config, item),
                EConfigType.Tuic => ConfigHandler.AddTuicServer(_config, item),
                EConfigType.Wireguard => ConfigHandler.AddWireguardServer(_config, item),
                _ => -1,
            };

            if (ret == 0)
            {
                _noticeHandler?.Enqueue(ResUI.OperationSuccess);
                _view.DialogResult = true;
                //_view?.Close();
            }
            else
            {
                UI.Show(ResUI.OperationFailed);
            }
        }
    }
}