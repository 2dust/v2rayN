using System.Reactive;

namespace ServiceLib.Events;

public static class AppEvents
{
    public static readonly EventChannel<Unit> ReloadRequested = new();
    public static readonly EventChannel<bool?> ShowHideWindowRequested = new();
    public static readonly EventChannel<Unit> AddServerViaScanRequested = new();
    public static readonly EventChannel<Unit> AddServerViaClipboardRequested = new();
    public static readonly EventChannel<bool> SubscriptionsUpdateRequested = new();

    public static readonly EventChannel<Unit> ProfilesRefreshRequested = new();
    public static readonly EventChannel<Unit> SubscriptionsRefreshRequested = new();
    public static readonly EventChannel<Unit> ProxiesReloadRequested = new();
    public static readonly EventChannel<ServerSpeedItem> DispatcherStatisticsRequested = new();

    public static readonly EventChannel<string> SendSnackMsgRequested = new();
    public static readonly EventChannel<string> SendMsgViewRequested = new();

    public static readonly EventChannel<Unit> AppExitRequested = new();
    public static readonly EventChannel<bool> ShutdownRequested = new();

    public static readonly EventChannel<Unit> AdjustMainLvColWidthRequested = new();

    public static readonly EventChannel<string> SetDefaultServerRequested = new();

    public static readonly EventChannel<Unit> RoutingsMenuRefreshRequested = new();
    public static readonly EventChannel<Unit> TestServerRequested = new();
    public static readonly EventChannel<Unit> InboundDisplayRequested = new();
    public static readonly EventChannel<ESysProxyType> SysProxyChangeRequested = new();
}
