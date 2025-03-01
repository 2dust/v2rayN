using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace v2rayN.Desktop.Views
{
    public partial class QrcodeView : UserControl
    {
        public QrcodeView()
        {
            InitializeComponent();
        }

        public QrcodeView(string? url)
        {
            InitializeComponent();

            txtContent.Text = url;
            imgQrcode.Source = GetQRCode(url);

            txtContent.GotFocus += (_, _) => Dispatcher.UIThread.Post(() => { txtContent.SelectAll(); });
        }

        private Bitmap? GetQRCode(string? url)
        {
            var bytes = QRCodeHelper.GenQRCode(url);
            return ByteToBitmap(bytes);
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
}
