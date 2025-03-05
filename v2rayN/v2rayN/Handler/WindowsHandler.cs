using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace v2rayN.Handler
{
    public sealed class WindowsHandler
    {
        private static readonly Lazy<WindowsHandler> instance = new(() => new());
        public static WindowsHandler Instance => instance.Value;
        private static readonly string _tag = "WindowsHandler";

        public async Task<Icon> GetNotifyIcon(Config config)
        {
            try
            {
                var index = (int)config.SystemProxyItem.SysProxyType;

                //Load from routing setting
                var createdIcon = await GetNotifyIcon4Routing(config);
                if (createdIcon != null)
                {
                    return createdIcon;
                }

                //Load from local file
                var fileName = Utils.GetPath($"NotifyIcon{index + 1}.ico");
                if (File.Exists(fileName))
                {
                    return new Icon(fileName);
                }
                return index switch
                {
                    0 => Properties.Resources.NotifyIcon1,
                    1 => Properties.Resources.NotifyIcon2,
                    2 => Properties.Resources.NotifyIcon3,
                    3 => Properties.Resources.NotifyIcon4,
                    _ => Properties.Resources.NotifyIcon1, // default
                };
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                return Properties.Resources.NotifyIcon1;
            }
        }

        public System.Windows.Media.ImageSource GetAppIcon(Config config)
        {
            var index = (int)config.SystemProxyItem.SysProxyType + 1;
            return BitmapFrame.Create(new Uri($"pack://application:,,,/Resources/NotifyIcon{index}.ico", UriKind.RelativeOrAbsolute));
        }

        private async Task<Icon?> GetNotifyIcon4Routing(Config config)
        {
            try
            {
                var item = await ConfigHandler.GetDefaultRouting(config);
                if (item == null || item.CustomIcon.IsNullOrEmpty() || !File.Exists(item.CustomIcon))
                {
                    return null;
                }

                Color color = ColorTranslator.FromHtml("#3399CC");
                int index = (int)config.SystemProxyItem.SysProxyType;
                if (index > 0)
                {
                    color = (new[] { Color.Red, Color.Purple, Color.DarkGreen, Color.Orange, Color.DarkSlateBlue, Color.RoyalBlue })[index - 1];
                }

                int width = 128;
                int height = 128;

                Bitmap bitmap = new(width, height);
                Graphics graphics = Graphics.FromImage(bitmap);
                SolidBrush drawBrush = new(color);

                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                //graphics.FillRectangle(drawBrush, new Rectangle(0, 0, width, height));
                graphics.DrawImage(new Bitmap(item.CustomIcon), 0, 0, width, height);
                graphics.FillEllipse(drawBrush, width / 2, width / 2, width / 2, width / 2);

                Icon createdIcon = Icon.FromHandle(bitmap.GetHicon());

                drawBrush.Dispose();
                graphics.Dispose();
                bitmap.Dispose();

                return createdIcon;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
                return null;
            }
        }

        public void RegisterGlobalHotkey(Config config, Action<EGlobalHotkey> handler, Action<bool, string>? update)
        {
            HotkeyHandler.Instance.UpdateViewEvent += update;
            HotkeyHandler.Instance.HotkeyTriggerEvent += handler;
            HotkeyHandler.Instance.Load();
        }
    }
}