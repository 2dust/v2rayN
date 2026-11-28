using Avalonia.VisualTree;
using DialogHostAvalonia;

namespace v2rayN.Desktop.Views;

public partial class QrcodeView : UserControl
{
    public QrcodeView()
    {
        InitializeComponent();
        RegisterAccessibilityHandlers();
        if (Design.IsDesignMode)
        {
            //const string url = Global.AppName;
            const string url = Global.SystemProxyExceptionsWindows;
            txtContent.Text = url;
            imgQrcode.Source = GetQRCode(url);
        }
    }

    public QrcodeView(string? url)
    {
        InitializeComponent();
        RegisterAccessibilityHandlers();

        txtContent.Text = url;
        imgQrcode.Source = GetQRCode(url);

        txtContent.GotFocus += (_, _) => Dispatcher.UIThread.Post(() => { txtContent.SelectAll(); });
    }

    private void RegisterAccessibilityHandlers()
    {
        Loaded += (_, _) => txtContent.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                this.FindAncestorOfType<DialogHost>()?.CurrentSession?.Close();
                e.Handled = true;
            }
        };
    }

    private Bitmap? GetQRCode(string? url)
    {
        try
        {
            var bytes = QRCodeUtils.GenQRCode(url);
            return ByteToBitmap(bytes);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("GetQRCode", ex);
            return null;
        }
    }

    private Bitmap? ByteToBitmap(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }
}
