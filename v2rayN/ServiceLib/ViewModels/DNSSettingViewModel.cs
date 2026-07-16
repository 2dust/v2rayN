namespace ServiceLib.ViewModels;

public class DNSSettingViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive] public bool UseSystemHosts { get; set; }
    [Reactive] public bool AddCommonHosts { get; set; }
    [Reactive] public bool FakeIP { get; set; }
    [Reactive] public bool BlockBindingQuery { get; set; }
    [Reactive] public string DirectDNS { get; set; }
    [Reactive] public string RemoteDNS { get; set; }
    [Reactive] public string BootstrapDNS { get; set; }
    [Reactive] public string Strategy4Freedom { get; set; }
    [Reactive] public string Strategy4Proxy { get; set; }
    [Reactive] public string Strategy4ProxyDial { get; set; }
    [Reactive] public string Hosts { get; set; }
    [Reactive] public string DirectExpectedIPs { get; set; }
    [Reactive] public bool ParallelQuery { get; set; }
    [Reactive] public bool ServeStale { get; set; }
    [Reactive] public bool EnableHappyEyeballs { get; set; }

    [Reactive] public bool UseSystemHostsCompatible { get; set; }
    [Reactive] public string DomainStrategy4FreedomCompatible { get; set; } = string.Empty;
    [Reactive] public string DomainDNSAddressCompatible { get; set; } = string.Empty;
    [Reactive] public string NormalDNSCompatible { get; set; } = string.Empty;
    [Reactive] public string TunDNSCompatible { get; set; } = string.Empty;

    [Reactive] public string DomainStrategy4Freedom2Compatible { get; set; } = string.Empty;
    [Reactive] public string DomainDNSAddress2Compatible { get; set; } = string.Empty;
    [Reactive] public string NormalDNS2Compatible { get; set; } = string.Empty;
    [Reactive] public string TunDNS2Compatible { get; set; } = string.Empty;
    [Reactive] public bool RayCustomDNSEnableCompatible { get; set; }
    [Reactive] public bool SBCustomDNSEnableCompatible { get; set; }

    [ObservableAsProperty] public bool IsSimpleDNSEnabled { get; }

    public ReactiveCommand<Unit, Unit> SaveCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportDefConfig4V2rayCompatibleCmd { get; }
    public ReactiveCommand<Unit, Unit> ImportDefConfig4SingboxCompatibleCmd { get; }

    public DNSSettingViewModel()
    {
        _config = AppManager.Instance.Config;
        SaveCmd = ReactiveCommand.CreateFromTask(SaveSettingAsync);

        ImportDefConfig4V2rayCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNSCompatible = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
            TunDNSCompatible = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
            await Task.CompletedTask;
        });

        ImportDefConfig4SingboxCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNS2Compatible = EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName);
            TunDNS2Compatible = EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName);
            await Task.CompletedTask;
        });

        this.WhenAnyValue(x => x.RayCustomDNSEnableCompatible, x => x.SBCustomDNSEnableCompatible)
            .Select(x => x is not { Item1: true, Item2: true })
            .ToPropertyEx(this, x => x.IsSimpleDNSEnabled);

        _ = Init();
    }

    private async Task Init()
    {
        _config = AppManager.Instance.Config;
        var item = _config.SimpleDNSItem;
        UseSystemHosts = item.UseSystemHosts ?? false;
        AddCommonHosts = item.AddCommonHosts ?? false;
        FakeIP = item.FakeIP ?? false;
        BlockBindingQuery = item.BlockBindingQuery ?? false;
        DirectDNS = item.DirectDNS ?? string.Empty;
        RemoteDNS = item.RemoteDNS ?? string.Empty;
        BootstrapDNS = item.BootstrapDNS ?? string.Empty;
        Strategy4Freedom = item.Strategy4Freedom ?? string.Empty;
        Strategy4Proxy = item.Strategy4Proxy ?? string.Empty;
        Strategy4ProxyDial = item.Strategy4ProxyDial ?? string.Empty;
        Hosts = item.Hosts ?? string.Empty;
        DirectExpectedIPs = item.DirectExpectedIPs ?? string.Empty;
        ParallelQuery = item.ParallelQuery ?? false;
        ServeStale = item.ServeStale ?? false;
        EnableHappyEyeballs = item.EnableHappyEyeballs ?? false;
        var item1 = await AppManager.Instance.GetDNSItem(ECoreType.Xray);
        RayCustomDNSEnableCompatible = item1.Enabled;
        UseSystemHostsCompatible = item1.UseSystemHosts;
        DomainStrategy4FreedomCompatible = item1?.DomainStrategy4Freedom ?? string.Empty;
        DomainDNSAddressCompatible = item1?.DomainDNSAddress ?? string.Empty;
        NormalDNSCompatible = item1?.NormalDNS ?? string.Empty;
        TunDNSCompatible = item1?.TunDNS ?? string.Empty;

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
        _config.SimpleDNSItem.BootstrapDNS = BootstrapDNS;
        _config.SimpleDNSItem.Strategy4Freedom = Strategy4Freedom;
        _config.SimpleDNSItem.Strategy4Proxy = Strategy4Proxy;
        _config.SimpleDNSItem.Strategy4ProxyDial = Strategy4ProxyDial;
        _config.SimpleDNSItem.Hosts = Hosts;
        _config.SimpleDNSItem.DirectExpectedIPs = DirectExpectedIPs;
        _config.SimpleDNSItem.ParallelQuery = ParallelQuery;
        _config.SimpleDNSItem.ServeStale = ServeStale;
        _config.SimpleDNSItem.EnableHappyEyeballs = EnableHappyEyeballs;
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
        if (TunDNSCompatible.IsNotEmpty())
        {
            var obj = JsonUtils.ParseJson(TunDNSCompatible);
            if (obj != null && obj["servers"] != null)
            {
            }
            else
            {
                if (TunDNSCompatible.Contains('{') || TunDNSCompatible.Contains('}'))
                {
                    NoticeManager.Instance.Enqueue(ResUI.FillCorrectDNSText);
                    return;
                }
            }
        }
        if (NormalDNS2Compatible.IsNotEmpty())
        {
            var obj2 = JsonUtils.Deserialize<Dns4Sbox>(NormalDNS2Compatible);
            if (obj2 == null
                || obj2.servers.Count == 0
                || obj2.servers.Any(s => s.type.IsNullOrEmpty()))
            {
                NoticeManager.Instance.Enqueue(ResUI.FillCorrectDNSText);
                return;
            }
        }
        if (TunDNS2Compatible.IsNotEmpty())
        {
            var obj2 = JsonUtils.Deserialize<Dns4Sbox>(TunDNS2Compatible);
            if (obj2 == null
                || obj2.servers.Count == 0
                || obj2.servers.Any(s => s.type.IsNullOrEmpty()))
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
        item1.TunDNS = TunDNSCompatible;
        await ConfigHandler.SaveDNSItems(_config, item1);

        var item2 = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
        item2.Enabled = SBCustomDNSEnableCompatible;
        item2.DomainStrategy4Freedom = DomainStrategy4Freedom2Compatible;
        item2.DomainDNSAddress = DomainDNSAddress2Compatible;
        item2.NormalDNS = JsonUtils.Serialize(JsonUtils.Deserialize<Dns4Sbox>(NormalDNS2Compatible));
        item2.TunDNS = JsonUtils.Serialize(JsonUtils.Deserialize<Dns4Sbox>(TunDNS2Compatible));
        await ConfigHandler.SaveDNSItems(_config, item2);

        await ConfigHandler.SaveConfig(_config);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
