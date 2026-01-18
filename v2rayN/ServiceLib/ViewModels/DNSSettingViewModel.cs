namespace ServiceLib.ViewModels;

public class DNSSettingViewModel : MyReactiveObject
{
    [Reactive] public bool? UseSystemHosts { get; set; }
    [Reactive] public bool? AddCommonHosts { get; set; }
    [Reactive] public bool? FakeIP { get; set; }
    [Reactive] public bool? BlockBindingQuery { get; set; }
    [Reactive] public string? DirectDNS { get; set; }
    [Reactive] public string? RemoteDNS { get; set; }
    [Reactive] public string? BootstrapDNS { get; set; }
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

    [ObservableAsProperty] public bool IsSimpleDNSEnabled { get; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportDefConfig4V2rayCompatibleCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportDefConfig4SingboxCompatibleCmd { get; }

    public DNSSettingViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        Config = AppManager.Instance.Config;
        UpdateView = updateView;
        SaveCmd = ReactiveCommand.CreateFromTask(SaveSettingAsync);

        ImportDefConfig4V2rayCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNSCompatible = EmbedUtils.GetEmbedText(AppConfig.DNSV2rayNormalFileName);
            await Task.CompletedTask;
        });

        ImportDefConfig4SingboxCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNS2Compatible = EmbedUtils.GetEmbedText(AppConfig.DNSSingboxNormalFileName);
            TunDNS2Compatible = EmbedUtils.GetEmbedText(AppConfig.TunSingboxDNSFileName);
            await Task.CompletedTask;
        });

        this.WhenAnyValue(x => x.RayCustomDNSEnableCompatible, x => x.SBCustomDNSEnableCompatible)
            .Select(x => !(x.Item1 && x.Item2))
            .ToPropertyEx(this, x => x.IsSimpleDNSEnabled);

        _ = Init();
    }

    private async Task Init()
    {
        Config = AppManager.Instance.Config;
        var item = Config.SimpleDNSItem;
        UseSystemHosts = item.UseSystemHosts;
        AddCommonHosts = item.AddCommonHosts;
        FakeIP = item.FakeIP;
        BlockBindingQuery = item.BlockBindingQuery;
        DirectDNS = item.DirectDNS;
        RemoteDNS = item.RemoteDNS;
        BootstrapDNS = item.BootstrapDNS;
        RayStrategy4Freedom = item.RayStrategy4Freedom;
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
        Config.SimpleDNSItem.UseSystemHosts = UseSystemHosts;
        Config.SimpleDNSItem.AddCommonHosts = AddCommonHosts;
        Config.SimpleDNSItem.FakeIP = FakeIP;
        Config.SimpleDNSItem.BlockBindingQuery = BlockBindingQuery;
        Config.SimpleDNSItem.DirectDNS = DirectDNS;
        Config.SimpleDNSItem.RemoteDNS = RemoteDNS;
        Config.SimpleDNSItem.BootstrapDNS = BootstrapDNS;
        Config.SimpleDNSItem.RayStrategy4Freedom = RayStrategy4Freedom;
        Config.SimpleDNSItem.SingboxStrategy4Direct = SingboxStrategy4Direct;
        Config.SimpleDNSItem.SingboxStrategy4Proxy = SingboxStrategy4Proxy;
        Config.SimpleDNSItem.Hosts = Hosts;
        Config.SimpleDNSItem.DirectExpectedIPs = DirectExpectedIPs;

        if (NormalDNSCompatible.IsNotEmpty())
        {
            var obj = JsonUtils.ParseJson(NormalDNSCompatible);
            if (obj is not null && obj["servers"] is not null)
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
            if (obj2 is null)
            {
                NoticeManager.Instance.Enqueue(ResUI.FillCorrectDNSText);
                return;
            }
        }
        if (TunDNS2Compatible.IsNotEmpty())
        {
            var obj2 = JsonUtils.Deserialize<Dns4Sbox>(TunDNS2Compatible);
            if (obj2 is null)
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
        await ConfigHandler.SaveDNSItems(Config, item1);

        var item2 = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
        item2.Enabled = SBCustomDNSEnableCompatible;
        item2.DomainStrategy4Freedom = DomainStrategy4Freedom2Compatible;
        item2.DomainDNSAddress = DomainDNSAddress2Compatible;
        item2.NormalDNS = JsonUtils.Serialize(JsonUtils.ParseJson(NormalDNS2Compatible));
        item2.TunDNS = JsonUtils.Serialize(JsonUtils.ParseJson(TunDNS2Compatible));
        await ConfigHandler.SaveDNSItems(Config, item2);

        await ConfigHandler.SaveConfig(Config);
        if (UpdateView is not null)
        {
            await UpdateView(EViewAction.CloseWindow, null);
        }
    }
}
