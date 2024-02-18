﻿using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Model;
using v2rayN.Resx;

namespace v2rayN.ViewModels
{
    public class DNSSettingViewModel : ReactiveObject
    {
        private static Config _config;
        private NoticeHandler? _noticeHandler;
        private Window _view;

        [Reactive] public bool useSystemHosts { get; set; }
        [Reactive] public string domainStrategy4Freedom { get; set; }
        [Reactive] public string normalDNS { get; set; }
        [Reactive] public string normalDNS2 { get; set; }
        [Reactive] public string tunDNS2 { get; set; }

        public ReactiveCommand<Unit, Unit> SaveCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportDefConfig4V2rayCmd { get; }
        public ReactiveCommand<Unit, Unit> ImportDefConfig4SingboxCmd { get; }

        public DNSSettingViewModel(Window view)
        {
            _config = LazyConfig.Instance.GetConfig();
            _noticeHandler = Locator.Current.GetService<NoticeHandler>();
            _view = view;

            var item = LazyConfig.Instance.GetDNSItem(ECoreType.Xray);
            useSystemHosts = item.useSystemHosts;
            domainStrategy4Freedom = item?.domainStrategy4Freedom!;
            normalDNS = item?.normalDNS!;

            var item2 = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
            normalDNS2 = item2?.normalDNS!;
            tunDNS2 = item2?.tunDNS!;

            SaveCmd = ReactiveCommand.Create(() =>
            {
                SaveSetting();
            });

            ImportDefConfig4V2rayCmd = ReactiveCommand.Create(() =>
            {
                normalDNS = Utile.GetEmbedText(Global.DNSV2rayNormalFileName);
            });

            ImportDefConfig4SingboxCmd = ReactiveCommand.Create(() =>
            {
                normalDNS2 = Utile.GetEmbedText(Global.DNSSingboxNormalFileName);
                tunDNS2 = Utile.GetEmbedText(Global.TunSingboxDNSFileName);
            });

            Utile.SetDarkBorder(view, _config.uiItem.colorModeDark);
        }

        private void SaveSetting()
        {
            if (!Utile.IsNullOrEmpty(normalDNS))
            {
                var obj = JsonUtile.ParseJson(normalDNS);
                if (obj != null && obj["servers"] != null)
                {
                }
                else
                {
                    if (normalDNS.Contains("{") || normalDNS.Contains("}"))
                    {
                        UI.Show(ResUI.FillCorrectDNSText);
                        return;
                    }
                }
            }
            if (!Utile.IsNullOrEmpty(normalDNS2))
            {
                var obj2 = JsonUtile.Deserialize<Dns4Sbox>(normalDNS2);
                if (obj2 == null)
                {
                    UI.Show(ResUI.FillCorrectDNSText);
                    return;
                }
            }
            if (!Utile.IsNullOrEmpty(tunDNS2))
            {
                var obj2 = JsonUtile.Deserialize<Dns4Sbox>(tunDNS2);
                if (obj2 == null)
                {
                    UI.Show(ResUI.FillCorrectDNSText);
                    return;
                }
            }

            var item = LazyConfig.Instance.GetDNSItem(ECoreType.Xray);
            item.domainStrategy4Freedom = domainStrategy4Freedom;
            item.useSystemHosts = useSystemHosts;
            item.normalDNS = normalDNS;
            ConfigHandler.SaveDNSItems(_config, item);

            var item2 = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
            item2.normalDNS = JsonUtile.Serialize(JsonUtile.ParseJson(normalDNS2));
            item2.tunDNS = JsonUtile.Serialize(JsonUtile.ParseJson(tunDNS2));
            ConfigHandler.SaveDNSItems(_config, item2);

            _noticeHandler?.Enqueue(ResUI.OperationSuccess);
            _view.DialogResult = true;
        }
    }
}