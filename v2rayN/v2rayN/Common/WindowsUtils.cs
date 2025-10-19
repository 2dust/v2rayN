using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace v2rayN;

internal static class WindowsUtils
{
    private static readonly string _tag = "WindowsUtils";

    public static string? GetClipboardData()
    {
        var strData = string.Empty;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data?.GetDataPresent(DataFormats.UnicodeText) == true)
            {
                strData = data.GetData(DataFormats.UnicodeText)?.ToString();
            }
            return strData;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return strData;
    }

    public static void SetClipboardData(string strData)
    {
        try
        {
            Clipboard.SetText(strData);
        }
        catch
        {
        }
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int attributeValue, uint attributeSize);

    public static ImageSource IconToImageSource(Icon icon)
    {
        return Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            new System.Windows.Int32Rect(0, 0, icon.Width, icon.Height),
            BitmapSizeOptions.FromEmptyOptions());
    }

    public static void SetDarkBorder(Window window, string? theme)
    {
        var isDark = theme switch
        {
            nameof(ETheme.Dark) => true,
            nameof(ETheme.Light) => false,
            _ => IsDarkTheme(),
        };

        SetDarkBorder(window, isDark);
    }

    private static void SetDarkBorder(Window window, bool dark)
    {
        // Make sure the handle is created before the window is shown
        IntPtr hWnd = new WindowInteropHelper(window).EnsureHandle();
        int attribute = dark ? 1 : 0;
        uint attributeSize = (uint)Marshal.SizeOf(attribute);
        DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
        DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
    }

    private static bool IsDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var obj = key?.GetValue("AppsUseLightTheme");
        int.TryParse(obj?.ToString(), out var value);
        return value == 0;
    }

    #region Windows API

    [Flags]
    public enum DWMWINDOWATTRIBUTE : uint
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
    }

    #endregion Windows API
}
