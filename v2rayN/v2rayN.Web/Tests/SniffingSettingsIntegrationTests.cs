using System.Reflection;
using System.Text.Json.Nodes;
using ServiceLib;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Models;
using ServiceLib.Models.Configs;
using ServiceLib.Models.CoreConfigs;
using ServiceLib.Models.Entities;
using ServiceLib.Manager;
using ServiceLib.Services.CoreConfig;
using v2rayN.Web.Contracts;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

[NotInParallel]
public class SniffingSettingsIntegrationTests
{
    [Test]
    public async Task WebInboundInputEnablesXraySniffingAndUsesServiceLibDefaultsWhenOverrideIsEmpty()
    {
        var config = CreateConfig();
        BindAppManagerConfig(config);
        var input = CreateInboundInput(sniffingEnabled: true, destOverride: []);
        V2rayRuntime.ApplyInboundSettings(config.Inbound[0], input);

        var generated = Generate(ECoreType.Xray, config);
        var sniffing = generated["inbounds"]![0]!["sniffing"]!;

        await sniffing["enabled"]!.GetValue<bool>().Should().BeTrue();
        await sniffing["destOverride"]![0]!.GetValue<string>().Should().BeEqualTo("http");
        await sniffing["destOverride"]![1]!.GetValue<string>().Should().BeEqualTo("tls");
        await config.Inbound[0].SniffingEnabled.Should().BeTrue();

        var runtime = new V2rayRuntime(null!, null!, null!, null!, null!);
        var reloadedSettings = await runtime.GetSettingsAsync();
        await reloadedSettings.Inbound.SniffingEnabled.Should().BeTrue();
        await reloadedSettings.Inbound.DestOverride.SequenceEqual(new[] { "http", "tls" }).Should().BeTrue();
    }

    [Test]
    public async Task WebInboundInputEnablesSingboxSniffAction()
    {
        var config = CreateConfig();
        BindAppManagerConfig(config);
        var input = CreateInboundInput(sniffingEnabled: true, destOverride: []);
        V2rayRuntime.ApplyInboundSettings(config.Inbound[0], input);

        var generated = Generate(ECoreType.sing_box, config);
        var routeRules = generated["route"]!["rules"]!.AsArray();

        await routeRules.Any(rule => rule?["action"]?.GetValue<string>() == "sniff").Should().BeTrue();
        await config.Inbound[0].SniffingEnabled.Should().BeTrue();
    }

    [Test]
    public async Task EnablingSniffingPreservesExplicitProtocolsAndRunningCoreChangeRequestsOneRestart()
    {
        var config = CreateConfig();
        var input = CreateInboundInput(sniffingEnabled: true, destOverride: ["tls"]);
        var normalized = V2rayRuntime.NormalizeDestOverride(input.SniffingEnabled, input.DestOverride);
        await normalized.SequenceEqual(new[] { "tls" }).Should().BeTrue();

        var coreChanged = config.Inbound[0].SniffingEnabled != input.SniffingEnabled
            || !config.Inbound[0].DestOverride.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase);
        var restartCount = 0;
        var execution = await CoreSettingsApplyExecutor.ExecuteAsync(
            CoreRuntimeState.Running,
            hasActiveChild: true,
            changed: coreChanged,
            () =>
            {
                restartCount++;
                return Task.FromResult(OperationView.Ok(ApiMessageKeys.CommonCompleted));
            });

        await coreChanged.Should().BeTrue();
        await execution.Action.Should().BeEqualTo(CoreSettingsApplyAction.Restart);
        await restartCount.Should().BeEqualTo(1);
    }

    [Test]
    public async Task EmptyDestOverrideNormalizationIsIdempotentAcrossRepeatedStaleSettingsSaves()
    {
        var config = CreateConfig();
        BindAppManagerConfig(config);
        var stalePayload = CreateInboundInput(sniffingEnabled: true, destOverride: []);
        var runtime = new V2rayRuntime(null!, null!, null!, null!, null!);

        // First save flips sniffing on, and the Web-owned inbound apply path supplies
        // ServiceLib's defaults even though the submitted protocols remain empty.
        V2rayRuntime.ApplyInboundSettings(config.Inbound[0], stalePayload);
        var afterFirstSave = await runtime.GetSettingsAsync();
        await afterFirstSave.Inbound.SniffingEnabled.Should().BeTrue();
        await afterFirstSave.Inbound.DestOverride.SequenceEqual(new[] { "http", "tls" }).Should().BeTrue();

        // The browser still submits its stale empty array. A subsequent save must not
        // erase the canonical backend defaults just because sniffing was already enabled.
        V2rayRuntime.ApplyInboundSettings(config.Inbound[0], stalePayload);
        var afterSecondSave = await runtime.GetSettingsAsync();
        await afterSecondSave.Inbound.SniffingEnabled.Should().BeTrue();
        await afterSecondSave.Inbound.DestOverride.SequenceEqual(new[] { "http", "tls" }).Should().BeTrue();

        var generated = Generate(ECoreType.Xray, config);
        var sniffing = generated["inbounds"]![0]!["sniffing"]!;
        await sniffing["enabled"]!.GetValue<bool>().Should().BeTrue();
        await sniffing["destOverride"]![0]!.GetValue<string>().Should().BeEqualTo("http");
        await sniffing["destOverride"]![1]!.GetValue<string>().Should().BeEqualTo("tls");
    }

    [Test]
    public async Task DestOverrideNormalizationPreservesExplicitAndDisabledValues()
    {
        var defaults = new InItem().DestOverride?.ToArray() ?? [];
        var falseToTrue = V2rayRuntime.NormalizeDestOverride(true, []);
        var trueToTrue = V2rayRuntime.NormalizeDestOverride(true, []);
        var explicitProtocols = V2rayRuntime.NormalizeDestOverride(true, ["tls"]);
        var disabledEmpty = V2rayRuntime.NormalizeDestOverride(false, []);
        var disabledExisting = V2rayRuntime.NormalizeDestOverride(false, ["http", "tls"]);

        await falseToTrue.SequenceEqual(defaults).Should().BeTrue();
        await trueToTrue.SequenceEqual(defaults).Should().BeTrue();
        await explicitProtocols.SequenceEqual(new[] { "tls" }).Should().BeTrue();
        await disabledEmpty.Length.Should().BeEqualTo(0);
        await disabledExisting.SequenceEqual(new[] { "http", "tls" }).Should().BeTrue();
    }

    private static InboundSettingsInput CreateInboundInput(bool sniffingEnabled, string[] destOverride) => new(
        10808, false, true, sniffingEnabled, destOverride, false, false, false, string.Empty, string.Empty);

    private static JsonNode Generate(ECoreType coreType, Config config)
    {
        var node = CreateVmessNode(coreType);
        var context = new CoreConfigContext
        {
            Node = node,
            RunCoreType = coreType,
            RoutingItem = new RoutingItem
            {
                Id = "route-id",
                Remarks = "default",
                RuleSet = "[]",
                DomainStrategy = Global.AsIs,
                DomainStrategy4Singbox = string.Empty,
            },
            RawDnsItem = null,
            SimpleDnsItem = config.SimpleDNSItem,
            AllProxiesMap = new Dictionary<string, ProfileItem> { [node.IndexId] = node },
            AppConfig = config,
            FullConfigTemplate = null,
            IsTunEnabled = false,
            ProtectDomainList = [],
            HasGlobalIPv6Address = true,
        };
        var result = coreType == ECoreType.sing_box
            ? new CoreConfigSingboxService(context).GenerateClientConfigContent()
            : new CoreConfigV2rayService(context).GenerateClientConfigContent();
        if (result.Success != true || result.Data is null)
        {
            throw new InvalidOperationException($"Core configuration generation failed: {result.Msg}");
        }
        return JsonNode.Parse(result.Data.ToString()!)!;
    }

    internal static void BindAppManagerConfig(Config config)
    {
        var field = typeof(AppManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(AppManager.Instance, config);
    }

    private static ProfileItem CreateVmessNode(ECoreType coreType)
    {
        var node = new ProfileItem
        {
            IndexId = "sniffing-test-node",
            ConfigType = EConfigType.VMess,
            CoreType = coreType,
            Remarks = "sniffing test",
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
            Subid = string.Empty,
        };
        node.SetProtocolExtra(node.GetProtocolExtra() with { AlterId = "0", VmessSecurity = Global.DefaultSecurity });
        return node;
    }

    internal static Config CreateConfig() => new()
    {
        CoreBasicItem = new CoreBasicItem { Loglevel = "warning" },
        TunModeItem = new TunModeItem { EnableTun = false, IcmpRouting = "default" },
        KcpItem = new KcpItem(),
        GrpcItem = new GrpcItem(),
        RoutingBasicItem = new RoutingBasicItem { DomainStrategy = Global.AsIs, DomainStrategy4Singbox = string.Empty, RoutingIndexId = string.Empty },
        GuiItem = new GUIItem { EnableStatistics = false, DisplayRealTimeSpeed = false, EnableLog = false },
        MsgUIItem = new MsgUIItem(),
        UiItem = new UIItem { CurrentLanguage = "en", CurrentFontFamily = "sans", MainColumnItem = [], WindowSizeItem = [] },
        ConstItem = new ConstItem(),
        SpeedTestItem = new SpeedTestItem
        {
            SpeedPingTestUrl = Global.SpeedPingTestUrls.First(),
            SpeedTestUrl = Global.SpeedTestUrls.First(),
            SpeedTestTimeout = 10,
            MixedConcurrencyCount = 1,
            IPAPIUrl = string.Empty,
        },
        Mux4RayItem = new Mux4RayItem { Concurrency = 8, XudpConcurrency = 16, XudpProxyUDP443 = "reject" },
        Mux4SboxItem = new Mux4SboxItem { Protocol = Global.SingboxMuxs.First(), MaxConnections = 8 },
        HysteriaItem = new HysteriaItem { UpMbps = 100, DownMbps = 100 },
        ClashUIItem = new ClashUIItem { ConnectionsColumnItem = [] },
        SystemProxyItem = new SystemProxyItem { SystemProxyExceptions = string.Empty, SystemProxyAdvancedProtocol = string.Empty },
        WebDavItem = new WebDavItem(),
        CheckUpdateItem = new CheckUpdateItem(),
        Fragment4RayItem = new Fragment4RayItem { Packets = "tlshello", Lengths = ["100-200"], Delays = ["10-20"] },
        Inbound =
        [
            new InItem
            {
                Protocol = nameof(EInboundProtocol.socks),
                LocalPort = 10808,
                UdpEnabled = true,
                SniffingEnabled = false,
                RouteOnly = false,
                DestOverride = [],
            },
        ],
        GlobalHotkeys = [],
        CoreTypeItem = [new CoreTypeItem { ConfigType = EConfigType.VMess, CoreType = ECoreType.Xray }],
        SimpleDNSItem = ConfigHandler.InitBuiltinSimpleDNS(),
        HappyEyeballs4RayItem = new HappyEyeballs4RayItem(),
        IndexId = string.Empty,
        SubIndexId = string.Empty,
    };
}
