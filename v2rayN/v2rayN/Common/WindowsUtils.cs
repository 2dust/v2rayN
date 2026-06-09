using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace v2rayN.Common;

internal static partial class WindowsUtils
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

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, DWMWINDOWATTRIBUTE attribute, ref int attributeValue, uint attributeSize);

    public static ImageSource IconToImageSource(Icon icon)
    {
        return Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            new Int32Rect(0, 0, icon.Width, icon.Height),
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
        var hWnd = new WindowInteropHelper(window).EnsureHandle();
        var attribute = dark ? 1 : 0;
        var attributeSize = (uint)Marshal.SizeOf(attribute);
        DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attribute, attributeSize);
        DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attribute, attributeSize);
    }

    private static bool IsDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var obj = key?.GetValue("AppsUseLightTheme");
        var value = obj?.ToString().ToInt();
        return value == 0;
    }

    [LibraryImport("user32.dll")]
    private static partial int GetWindowLongW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowLongW(nint hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static void RemoveMinMaxButtons(Window window)
    {
        var hWnd = new WindowInteropHelper(window).EnsureHandle();
        var style = GetWindowLongW(hWnd, GWL_STYLE);
        if ((style & (WS_MINIMIZEBOX | WS_MAXIMIZEBOX)) != 0)
        {
            SetWindowLongW(hWnd, GWL_STYLE, style & ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX));
            SetWindowPos(hWnd, 0, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
    }

    #region Windows API

    private const int GWL_STYLE = -16;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int SWP_FRAMECHANGED = 0x0020;

    [Flags]
    public enum DWMWINDOWATTRIBUTE : uint
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
    }

    #endregion Windows API
}
