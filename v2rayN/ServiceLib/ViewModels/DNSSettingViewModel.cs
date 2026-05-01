namespace ServiceLib.ViewModels;

public partial class DNSSettingViewModel : MyReactiveObject
{
    [Reactive] public partial bool? UseSystemHosts { get; set; }
    [Reactive] public partial bool? AddCommonHosts { get; set; }
    [Reactive] public partial bool? FakeIP { get; set; }
    [Reactive] public partial bool? BlockBindingQuery { get; set; }
    [Reactive] public partial string? DirectDNS { get; set; }
    [Reactive] public partial string? RemoteDNS { get; set; }
    [Reactive] public partial string? BootstrapDNS { get; set; }
    [Reactive] public partial string? Strategy4Freedom { get; set; }
    [Reactive] public partial string? Strategy4Proxy { get; set; }
    [Reactive] public partial string? Hosts { get; set; }
    [Reactive] public partial string? DirectExpectedIPs { get; set; }
    [Reactive] public partial bool? ParallelQuery { get; set; }
    [Reactive] public partial bool? ServeStale { get; set; }

    [Reactive] public partial bool UseSystemHostsCompatible { get; set; }
    [Reactive] public partial string DomainStrategy4FreedomCompatible { get; set; }
    [Reactive] public partial string DomainDNSAddressCompatible { get; set; }
    [Reactive] public partial string NormalDNSCompatible { get; set; }
    [Reactive] public partial string TunDNSCompatible { get; set; }

    [Reactive] public partial string DomainStrategy4Freedom2Compatible { get; set; }
    [Reactive] public partial string DomainDNSAddress2Compatible { get; set; }
    [Reactive] public partial string NormalDNS2Compatible { get; set; }
    [Reactive] public partial string TunDNS2Compatible { get; set; }
    [Reactive] public partial bool RayCustomDNSEnableCompatible { get; set; }
    [Reactive] public partial bool SBCustomDNSEnableCompatible { get; set; }

    [ObservableAsProperty] private bool _isSimpleDNSEnabled;

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
            TunDNSCompatible = EmbedUtils.GetEmbedText(Global.DNSV2rayNormalFileName);
            await Task.CompletedTask;
        });

        ImportDefConfig4SingboxCompatibleCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            NormalDNS2Compatible = EmbedUtils.GetEmbedText(Global.DNSSingboxNormalFileName);
            TunDNS2Compatible = EmbedUtils.GetEmbedText(Global.TunSingboxDNSFileName);
            await Task.CompletedTask;
        });

        _isSimpleDNSEnabledHelper = this.WhenAnyValue(x => x.RayCustomDNSEnableCompatible, x => x.SBCustomDNSEnableCompatible)
            .Select(x => !(x.Item1 && x.Item2))
            .ToProperty(this, nameof(IsSimpleDNSEnabled));

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
        BootstrapDNS = item.BootstrapDNS;
        Strategy4Freedom = item.Strategy4Freedom;
        Strategy4Proxy = item.Strategy4Proxy;
        Hosts = item.Hosts;
        DirectExpectedIPs = item.DirectExpectedIPs;
        ParallelQuery = item.ParallelQuery;
        ServeStale = item.ServeStale;

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
        _config.SimpleDNSItem.Hosts = Hosts;
        _config.SimpleDNSItem.DirectExpectedIPs = DirectExpectedIPs;
        _config.SimpleDNSItem.ParallelQuery = ParallelQuery;
        _config.SimpleDNSItem.ServeStale = ServeStale;

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
        if (_updateView != null)
        {
            await _updateView(EViewAction.CloseWindow, null);
        }
    }
}
