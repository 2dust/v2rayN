using Tmds.DBus.Protocol;

namespace v2rayN.Desktop.Common;

/// <summary>
/// 会话里是否有可用的托盘。
/// Linux 桌面的托盘由 StatusNotifierWatcher 提供（Plasma、Cinnamon、waybar 等都实现了它），
/// 没有它的桌面（例如未安装扩展的 GNOME），Avalonia 的托盘图标不会显示出来，
/// 这时再把主窗口“隐藏到托盘”就会让窗口找不回来，所以要先检测。
/// </summary>
public static class TrayAvailability
{
    private const string StatusNotifierWatcher = "org.kde.StatusNotifierWatcher";
    private const string StatusNotifierWatcherPath = "/StatusNotifierWatcher";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    /// <summary>启动时调用一次，结果缓存在 <c>AppManager.IsTrayAvailable</c>。</summary>
    public static bool IsAvailable()
    {
        if (!Utils.IsLinux())
        {
            return false;
        }

        try
        {
            var connection = DBusConnection.Session;
            var services = connection.ListServicesAsync()
                .WaitAsync(TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();

            return services.Contains(StatusNotifierWatcher);
        }
        catch (Exception ex)
        {
            // 没有会话总线、dbus 连接异常等：按“没有托盘”处理，保持原来的最小化行为
            Logging.SaveLog("TrayAvailability", ex);
            return false;
        }
    }
}
