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

    public static WindowIcon GetAppIcon(
        ESysProxyType sysProxyType,
        bool isTunActive,
        bool isSystemProxySuppressedByTun)
    {
        var index = isTunActive
            ? !isSystemProxySuppressedByTun && sysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac
                ? 6
                : 5
            : (int)sysProxyType + 1;
        var fileName = Utils.GetPath($"NotifyIcon{index}.ico");
        if (File.Exists(fileName))
        {
            return new(fileName);
        }

        var uri = new Uri(Path.Combine(Global.AvaAssets, $"NotifyIcon{index}.ico"));
        using var bitmap = new Bitmap(AssetLoader.Open(uri));
        return new(bitmap);
    }
}
