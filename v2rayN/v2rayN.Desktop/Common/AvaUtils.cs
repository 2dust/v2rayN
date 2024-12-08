using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace v2rayN.Desktop.Common
{
    internal class AvaUtils
    {
        public static async Task<string?> GetClipboardData(Window owner)
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
                if (clipboard == null) return null;
                return await clipboard.GetTextAsync();
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
                if (clipboard == null) return;
                var dataObject = new DataObject();
                dataObject.Set(DataFormats.Text, strData);
                await clipboard.SetDataObjectAsync(dataObject);
            }
            catch
            {
            }
        }

        public static WindowIcon GetAppIcon(ESysProxyType sysProxyType)
        {
            var index = (int)sysProxyType + 1;
            var uri = new Uri(Path.Combine(Global.AvaAssets, $"NotifyIcon{index}.ico"));
            using var bitmap = new Bitmap(AssetLoader.Open(uri));
            return new(bitmap);
        }
    }
}