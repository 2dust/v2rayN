using System.Reactive;
using System.Reactive.Subjects;

namespace ServiceLib.Handler;

public static class AppEvents
{
    public static readonly Subject<Unit> ReloadRequested = new();
    public static readonly Subject<bool?> ShowHideWindowRequested = new();
    public static readonly Subject<Unit> AddServerViaScanRequested = new();
    public static readonly Subject<Unit> AddServerViaClipboardRequested = new();
    public static readonly Subject<bool> SubscriptionsUpdateRequested = new();

    public static readonly Subject<Unit> ProfilesRefreshRequested = new();
    public static readonly Subject<Unit> SubscriptionsRefreshRequested = new();
    public static readonly Subject<Unit> ProxiesReloadRequested = new();
    public static readonly Subject<ServerSpeedItem> DispatcherStatisticsRequested = new();

    public static readonly Subject<string> SendSnackMsgRequested = new();
    public static readonly Subject<string> SendMsgViewRequested = new();

    public static readonly Subject<Unit> AppExitRequested = new();
    public static readonly Subject<bool> ShutdownRequested = new();

    public static readonly Subject<Unit> AdjustMainLvColWidthRequested = new();

    public static readonly Subject<string> SetDefaultServerRequested = new();

    public static readonly Subject<Unit> RoutingsMenuRefreshRequested = new();
    public static readonly Subject<Unit> TestServerRequested = new();
    public static readonly Subject<Unit> InboundDisplayRequested = new();
    public static readonly Subject<ESysProxyType> SysProxyChangeRequested = new();
}
