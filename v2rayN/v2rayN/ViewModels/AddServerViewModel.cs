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
                SelectedSource = Utils.DeepCopy(profileItem);
            }

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveServer();
            });

            Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void SaveServer()
        {
            if (Utils.IsNullOrEmpty(SelectedSource.remarks))
            {
                UI.Show(ResUI.PleaseFillRemarks);
                return;
            }

            if (Utils.IsNullOrEmpty(SelectedSource.address))
            {
                UI.Show(ResUI.FillServerAddress);
                return;
            }
            var port = SelectedSource.port.ToString();
            if (Utils.IsNullOrEmpty(port) || !Utils.IsNumberic(port)
                || SelectedSource.port <= 0 || SelectedSource.port >= Global.MaxPort)
            {
                UI.Show(ResUI.FillCorrectServerPort);
                return;
            }
            if (SelectedSource.configType == EConfigType.Shadowsocks)
            {
                if (Utils.IsNullOrEmpty(SelectedSource.id))
                {
                    UI.Show(ResUI.FillPassword);
                    return;
                }
                if (Utils.IsNullOrEmpty(SelectedSource.security))
                {
                    UI.Show(ResUI.PleaseSelectEncryption);
                    return;
                }
            }
            if (SelectedSource.configType != EConfigType.Socks)
            {
                if (Utils.IsNullOrEmpty(SelectedSource.id))
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

            int ret = -1;
            switch (item.configType)
            {
                case EConfigType.VMess:
                    ret = ConfigHandler.AddServer(_config, item);
                    break;

                case EConfigType.Shadowsocks:
                    ret = ConfigHandler.AddShadowsocksServer(_config, item);
                    break;

                case EConfigType.Socks:
                    ret = ConfigHandler.AddSocksServer(_config, item);
                    break;

                case EConfigType.VLESS:
                    ret = ConfigHandler.AddVlessServer(_config, item);
                    break;

                case EConfigType.Trojan:
                    ret = ConfigHandler.AddTrojanServer(_config, item);
                    break;

                case EConfigType.Hysteria2:
                    ret = ConfigHandler.AddHysteria2Server(_config, item);
                    break;

                case EConfigType.Tuic:
                    ret = ConfigHandler.AddTuicServer(_config, item);
                    break;
            }

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