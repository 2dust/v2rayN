using Avalonia.Controls;
using Avalonia.Media.Imaging;

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

            //  btnCancel.Click += (s, e) => this.Close();
        }

        private Bitmap? GetQRCode(string? url)
        {
            var bytes = QRCodeHelper.GenQRCode(url);
            return ByteToBitmap(bytes);
        }

        private Bitmap? ByteToBitmap(byte[]? bytes)
        {
            if (bytes is null) return null;

            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
    }
}