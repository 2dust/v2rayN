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

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }
    //public ReactiveCommand<Unit, Unit> ImportDefConfig4V2rayCmd { get; }
    //public ReactiveCommand<Unit, Unit> ImportDefConfig4SingboxCmd { get; }

    public DNSSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppHandler.Instance.Config;
        _updateView = updateView;
        SaveCmd = ReactiveCommand.CreateFromTask(SaveSettingAsync);

        //ImportDefConfig4V2rayCmd = ReactiveCommand.CreateFromTask(async () =>
        //{
        //    NormalDNS = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
        //    await Task.CompletedTask;
        //});

        //ImportDefConfig4SingboxCmd = ReactiveCommand.CreateFromTask(async () =>
        //{
        //    NormalDNS2 = EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName);
        //    TunDNS2 = EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName);
        //    await Task.CompletedTask;
        //});

        _ = Init();
    }

    private async Task Init()
    {
        _config = AppHandler.Instance.Config;
        var item = _config.DNSItem;
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
    }

    private async Task SaveSettingAsync()
    {
        _config.DNSItem.UseSystemHosts = UseSystemHosts;
        _config.DNSItem.AddCommonHosts = AddCommonHosts;
        _config.DNSItem.FakeIP = FakeIP;
        _config.DNSItem.BlockBindingQuery = BlockBindingQuery;
        _config.DNSItem.DirectDNS = DirectDNS;
        _config.DNSItem.RemoteDNS = RemoteDNS;
        _config.DNSItem.RayStrategy4Freedom = RayStrategy4Freedom;
        _config.DNSItem.SingboxOutboundsResolveDNS = SingboxOutboundsResolveDNS;
        _config.DNSItem.SingboxFinalResolveDNS = SingboxFinalResolveDNS;
        _config.DNSItem.SingboxStrategy4Direct = SingboxStrategy4Direct;
        _config.DNSItem.SingboxStrategy4Proxy = SingboxStrategy4Proxy;
        _config.DNSItem.Hosts = Hosts;
        await ConfigHandler.SaveConfig(_config);
        if (_updateView != null)
        {
            await _updateView(EViewAction.CloseWindow, null);
        }
    }
}
