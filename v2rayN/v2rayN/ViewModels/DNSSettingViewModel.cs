using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using System.Reactive;
using System.Windows;
using v2rayN.Handler;
using v2rayN.Mode;
using v2rayN.Resx;

namespace v2rayN.ViewModels;

public class DNSSettingViewModel : ReactiveObject
{
    private static Config _config;
    private NoticeHandler? _noticeHandler;
    private Window _view;

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
            normalDNS = Utils.GetEmbedText(Global.DNSV2rayNormalFileName);
        });

        ImportDefConfig4SingboxCmd = ReactiveCommand.Create(() =>
        {
            normalDNS2 = Utils.GetEmbedText(Global.DNSSingboxNormalFileName);
            tunDNS2 = Utils.GetEmbedText(Global.TunSingboxDNSFileName);
        });

        Utils.SetDarkBorder(view, _config.uiItem.colorModeDark);
    }

    private void SaveSetting()
    {
        if (!Utils.IsNullOrEmpty(normalDNS))
        {
            var obj = Utils.ParseJson(normalDNS);
            if (obj != null && obj.ContainsKey("servers") == true)
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
        if (!Utils.IsNullOrEmpty(normalDNS2))
        {
            var obj2 = Utils.FromJson<Dns4Sbox>(normalDNS2);
            if (obj2 == null)
            {
                UI.Show(ResUI.FillCorrectDNSText);
                return;
            }
        }
        if (!Utils.IsNullOrEmpty(tunDNS2))
        {
            var obj2 = Utils.FromJson<Dns4Sbox>(tunDNS2);
            if (obj2 == null)
            {
                UI.Show(ResUI.FillCorrectDNSText);
                return;
            }
        }

        var item = LazyConfig.Instance.GetDNSItem(ECoreType.Xray);
        item.domainStrategy4Freedom = domainStrategy4Freedom;
        item.normalDNS = normalDNS;
        ConfigHandler.SaveDNSItems(_config, item);

        var item2 = LazyConfig.Instance.GetDNSItem(ECoreType.sing_box);
        item2.normalDNS = Utils.ToJson(Utils.ParseJson(normalDNS2));
        item2.tunDNS = Utils.ToJson(Utils.ParseJson(tunDNS2));
        ConfigHandler.SaveDNSItems(_config, item2);

        _noticeHandler?.Enqueue(ResUI.OperationSuccess);
        _view.DialogResult = true;
    }
}