using Avalonia.Input.Platform;

namespace v2rayN.Desktop.Common;

internal class AvaUtils
{
    public static async Task<string?> GetClipboardData(Window owner)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
            if (clipboard == null)
            {
                return null;
            }

            return await clipboard.TryGetTextAsync();
        }
        catch
        {
            return null;
        }
    }

    public static async Task SetClipboardData(Visual? visual, string strData)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
            if (clipboard == null)
            {
                return;
            }

            await clipboard.SetTextAsync(strData);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Whether the OS currently uses a dark appearance.
    /// Tray icons are drawn by the system tray / menu bar, which follows the OS
    /// appearance rather than the application theme setting.
    /// </summary>
    public static bool IsDarkAppearance()
    {
        var themeVariant = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant;
        return themeVariant switch
        {
            PlatformThemeVariant.Dark => true,
            PlatformThemeVariant.Light => false,
            _ => Application.Current?.ActualThemeVariant == ThemeVariant.Dark,
        };
    }

    public static WindowIcon GetAppIcon(ESysProxyType sysProxyType)
    {
        var index = (int)sysProxyType + 1;

        // An appearance specific override wins, so a monochrome icon can be supplied
        // for the dark tray and a contrasting one for the light tray.
        var appearance = IsDarkAppearance() ? "dark" : "light";
        foreach (var name in new[] { $"NotifyIcon{index}-{appearance}.ico", $"NotifyIcon{index}.ico" })
        {
            var fileName = Utils.GetPath(name);
            if (File.Exists(fileName))
            {
                return new(fileName);
            }
        }

        var uri = new Uri(Path.Combine(Global.AvaAssets, $"NotifyIcon{index}.ico"));
        using var bitmap = new Bitmap(AssetLoader.Open(uri));
        return new(bitmap);
    }
}
