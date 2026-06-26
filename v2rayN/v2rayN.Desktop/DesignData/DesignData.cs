using ServiceLib.Models.Entities;
using System.Diagnostics;

namespace v2rayN.Desktop.DesignData;

/// <summary>
/// Provides design-time data for Avalonia XAML previewer.
/// Each inner class lazily initializes <see cref="AppManager"/> with a stub config
/// so that ViewModel constructors don't fail during design-time rendering.
/// </summary>
public static class DesignData
{
    // ── Parameterless-constructor ViewModels ───────────────────────────────

    public static MainWindowViewModel? MainWindow { get; } = SafeCreate(() => new MainWindowViewModel());

    public static ProfilesViewModel? Profiles { get; } = SafeCreate(CreateProfiles);

    public static StatusBarViewModel? StatusBar { get; } = SafeCreate(CreateStatusBar);

    public static MsgViewModel? Msg { get; } = SafeCreate(() => new MsgViewModel());

    public static SubSettingViewModel? SubSetting { get; } = SafeCreate(() => new SubSettingViewModel());

    public static RoutingSettingViewModel? RoutingSetting { get; } = SafeCreate(() => new RoutingSettingViewModel());

    public static ClashProxiesViewModel? ClashProxies { get; } = SafeCreate(() => new ClashProxiesViewModel());

    public static ClashConnectionsViewModel? ClashConnections { get; } = SafeCreate(() => new ClashConnectionsViewModel());

    public static CheckUpdateViewModel? CheckUpdate { get; } = SafeCreate(() => new CheckUpdateViewModel());

    public static DNSSettingViewModel? DNSSetting { get; } = SafeCreate(() => new DNSSettingViewModel());

    public static FullConfigTemplateViewModel? FullConfigTemplate { get; } = SafeCreate(() => new FullConfigTemplateViewModel());

    public static GlobalHotkeySettingViewModel? GlobalHotkeySetting { get; } = SafeCreate(() => new GlobalHotkeySettingViewModel());

    public static OptionSettingViewModel? OptionSetting { get; } = SafeCreate(() => new OptionSettingViewModel());

    public static ProfilesSelectViewModel? ProfilesSelect { get; } = SafeCreate(() => new ProfilesSelectViewModel());

    public static BackupAndRestoreViewModel? BackupAndRestore { get; } = SafeCreate(() => new BackupAndRestoreViewModel());

    // ── ViewModels that require constructor parameters ─────────────────────

    public static AddGroupServerViewModel? AddGroupServer { get; } = SafeCreate(() => new AddGroupServerViewModel(new ProfileItem { Remarks = "Design Group", ConfigType = EConfigType.PolicyGroup }));

    public static AddServer2ViewModel? AddServer2 { get; } = SafeCreate(() => new AddServer2ViewModel(new ProfileItem { Remarks = "Design Custom Server", ConfigType = EConfigType.Custom }));

    public static AddServerViewModel? AddServer { get; } = SafeCreate(() => new AddServerViewModel(new ProfileItem { Remarks = "Design VMess Server", ConfigType = EConfigType.VMess, Address = "example.com", Port = 443 }));

    public static RoutingRuleSettingViewModel? RoutingRuleSetting { get; } = SafeCreate(() => new RoutingRuleSettingViewModel(new RoutingItem { Remarks = "Design Routing Rule" }));

    public static RoutingRuleDetailsViewModel? RoutingRuleDetails { get; } = SafeCreate(() => new RoutingRuleDetailsViewModel(new RulesItem { Domain = ["example.com"], OutboundTag = "direct" }));

    public static SubEditViewModel? SubEdit { get; } = SafeCreate(() => new SubEditViewModel(new SubItem { Remarks = "Design Subscription", Url = "https://example.com/sub" }));

    // ── Helper factories ───────────────────────────────────────────────────

    private static ProfilesViewModel CreateProfiles()
    {
        var vm = new ProfilesViewModel();
        vm.ProfileItems.Add(new ProfileItemModel
        {
            Remarks = "🚀 Design Server (Active)",
            ConfigType = EConfigType.VMess,
            Address = "design.example.com",
            Port = 443,
            Network = "ws",
            StreamSecurity = "tls",
            IsActive = true,
            DelayVal = "120ms",
        });
        vm.ProfileItems.Add(new ProfileItemModel
        {
            Remarks = "🔒 VLESS Server",
            ConfigType = EConfigType.VLESS,
            Address = "vless.example.com",
            Port = 443,
            Network = "grpc",
            StreamSecurity = "tls",
            DelayVal = "256ms",
        });
        vm.ProfileItems.Add(new ProfileItemModel
        {
            Remarks = "💧 Shadowsocks",
            ConfigType = EConfigType.Shadowsocks,
            Address = "ss.example.com",
            Port = 8388,
            Network = "tcp",
            DelayVal = "80ms",
        });
        vm.SubItems.Add(new SubItem { Remarks = "Default" });
        vm.SubItems.Add(new SubItem { Remarks = "My Subscription" });
        return vm;
    }

    private static StatusBarViewModel CreateStatusBar()
    {
        var vm = StatusBarViewModel.Instance;
        vm.InboundDisplay = "socks:10808";
        vm.InboundLanDisplay = "http:10809";
        vm.RunningServerDisplay = "🚀 Design Server (Active)";
        vm.RunningInfoDisplay = "v2rayN Design Mode";
        vm.SpeedProxyDisplay = "↑ 1.2 MB/s";
        vm.SpeedDirectDisplay = "↓ 5.6 MB/s";
        vm.RoutingItems.Add(new RoutingItem { Remarks = "Default Routing" });
        vm.RoutingItems.Add(new RoutingItem { Remarks = "Global" });
        return vm;
    }

    private static T? SafeCreate<T>(Func<T> factory) where T : class
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DesignData] Failed to create {typeof(T).Name}: {ex}");
            return null;
        }
    }
}
