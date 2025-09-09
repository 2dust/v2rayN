using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace ServiceLib.ViewModels;

public class DNSSettingViewModel : MyReactiveObject
{
    [Reactive] public bool? UseSystemHosts { get; set; }
    [Reactive] public bool? AddCommonHosts { get; set; }
    [Reactive] public bool? FakeIP { get; set; }
    [Reactive] public bool? BlockBindingQuery { get; set; }
    [Reactive] public string? DirectDNS { get; set; }
    [Reactive] public string? RemoteDNS { get; set; }
    [Reactive] public string? SingboxOutboundsResolveDNS { get; set; }
    [Reactive] public string? SingboxFinalResolveDNS { get; set; }
    [Reactive] public string? RayStrategy4Freedom { get; set; }
    [Reactive] public string? SingboxStrategy4Direct { get; set; }
    [Reactive] public string? SingboxStrategy4Proxy { get; set; }
    [Reactive] public string? Hosts { get; set; }
    [Reactive] public string? DirectExpectedIPs { get; set; }

    [Reactive] public bool UseSystemHostsCompatible { get; set; }
    [Reactive] public string DomainStrategy4FreedomCompatible { get; set; }
    [Reactive] public string DomainDNSAddressCompatible { get; set; }
    [Reactive] public string NormalDNSCompatible { get; set; }

    [Reactive] public string DomainStrategy4Freedom2Compatible { get; set; }
    [Reactive] public string DomainDNSAddress2Compatible { get; set; }
    [Reactive] public string NormalDNS2Compatible { get; set; }
    [Reactive] public string TunDNS2Compatible { get; set; }
    [Reactive] public bool RayCustomDNSEnableCompatible { get; set; }
    [Reactive] public bool SBCustomDNSEnableCompatible { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportDefConfig4V2rayCompatibleCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportDefConfig4SingboxCompatibleCmd { get; }

    public DNSSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;
        SaveCmd = ReactiveCommand.CreateFromTask(SaveSettingAsync);

        ImportDefConfig4V2rayCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNSCompatible = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
            await Task.CompletedTask;
        });

        ImportDefConfig4SingboxCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNS2Compatible = EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName);
            TunDNS2Compatible = EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName);
            await Task.CompletedTask;
        });

        _ = Init();
    }

    private async Task Init()
    {
        _config = AppManager.Instance.Config;
        var item = _config.SimpleDNSItem;
        UseSystemHosts = item.UseSystemHosts;
        AddCommonHosts = item.AddCommonHosts;
        FakeIP = item.FakeIP;
        BlockBindingQuery = item.BlockBindingQuery;
        DirectDNS = item.DirectDNS;
        RemoteDNS = item.RemoteDNS;
        RayStrategy4Freedom = item.RayStrategy4Freedom;
        SingboxOutboundsResolveDNS = item.SingboxOutboundsResolveDNS;
        SingboxFinalResolveDNS = item.SingboxFinalResolveDNS;
        SingboxStrategy4Direct = item.SingboxStrategy4Direct;
        SingboxStrategy4Proxy = item.SingboxStrategy4Proxy;
        Hosts = item.Hosts;
        DirectExpectedIPs = item.DirectExpectedIPs;

        var item1 = await AppManager.Instance.GetDNSItem(ECoreType.Xray);
        RayCustomDNSEnableCompatible = item1.Enabled;
        UseSystemHostsCompatible = item1.UseSystemHosts;
        DomainStrategy4FreedomCompatible = item1?.DomainStrategy4Freedom ?? string.Empty;
        DomainDNSAddressCompatible = item1?.DomainDNSAddress ?? string.Empty;
        NormalDNSCompatible = item1?.NormalDNS ?? string.Empty;

        var item2 = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
        SBCustomDNSEnableCompatible = item2.Enabled;
        DomainStrategy4Freedom2Compatible = item2?.DomainStrategy4Freedom ?? string.Empty;
        DomainDNSAddress2Compatible = item2?.DomainDNSAddress ?? string.Empty;
        NormalDNS2Compatible = item2?.NormalDNS ?? string.Empty;
        TunDNS2Compatible = item2?.TunDNS ?? string.Empty;
    }

    private async Task SaveSettingAsync()
    {
        _config.SimpleDNSItem.UseSystemHosts = UseSystemHosts;
        _config.SimpleDNSItem.AddCommonHosts = AddCommonHosts;
        _config.SimpleDNSItem.FakeIP = FakeIP;
        _config.SimpleDNSItem.BlockBindingQuery = BlockBindingQuery;
        _config.SimpleDNSItem.DirectDNS = DirectDNS;
        _config.SimpleDNSItem.RemoteDNS = RemoteDNS;
        _config.SimpleDNSItem.RayStrategy4Freedom = RayStrategy4Freedom;
        _config.SimpleDNSItem.SingboxOutboundsResolveDNS = SingboxOutboundsResolveDNS;
        _config.SimpleDNSItem.SingboxFinalResolveDNS = SingboxFinalResolveDNS;
        _config.SimpleDNSItem.SingboxStrategy4Direct = SingboxStrategy4Direct;
        _config.SimpleDNSItem.SingboxStrategy4Proxy = SingboxStrategy4Proxy;
        _config.SimpleDNSItem.Hosts = Hosts;
        _config.SimpleDNSItem.DirectExpectedIPs = DirectExpectedIPs;

        if (NormalDNSCompatible.IsNotEmpty())
        {
            var obj = JsonUtils.ParseJson(NormalDNSCompatible);
            if (obj != null && obj["servers"] != null)
            {
            }
            else
            {
                if (NormalDNSCompatible.Contains('{') || NormalDNSCompatible.Contains('}'))
                {
                    NoticeManager.Instance.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }
        }
        if (NormalDNS2Compatible.IsNotEmpty())
        {
            var obj2 = JsonUtils.Deserialize<Dns4Sbox>(NormalDNS2Compatible);
            if (obj2 == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.FillCorrectDNSText);
                return;
            }
        }
        if (TunDNS2Compatible.IsNotEmpty())
        {
            var obj2 = JsonUtils.Deserialize<Dns4Sbox>(TunDNS2Compatible);
            if (obj2 == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.FillCorrectDNSText);
                return;
            }
        }

        var item1 = await AppManager.Instance.GetDNSItem(ECoreType.Xray);
        item1.Enabled = RayCustomDNSEnableCompatible;
        item1.DomainStrategy4Freedom = DomainStrategy4FreedomCompatible;
        item1.DomainDNSAddress = DomainDNSAddressCompatible;
        item1.UseSystemHosts = UseSystemHostsCompatible;
        item1.NormalDNS = NormalDNSCompatible;
        await ConfigHandler.SaveDNSItems(_config, item1);

        var item2 = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
        item2.Enabled = SBCustomDNSEnableCompatible;
        item2.DomainStrategy4Freedom = DomainStrategy4Freedom2Compatible;
        item2.DomainDNSAddress = DomainDNSAddress2Compatible;
        item2.NormalDNS = JsonUtils.Serialize(JsonUtils.ParseJson(NormalDNS2Compatible));
        item2.TunDNS = JsonUtils.Serialize(JsonUtils.ParseJson(TunDNS2Compatible));
        await ConfigHandler.SaveDNSItems(_config, item2);

        await ConfigHandler.SaveConfig(_config);
        if (_updateView != null)
        {
            await _updateView(EViewAction.CloseWindow, null);
        }
    }
}
