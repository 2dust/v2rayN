using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace v2rayN.Desktop.Views
{
    public partial class QrcodeView : UserControl
    {
        public QrcodeView(string? url)
        {
            InitializeComponent();

            txtContent.Text = url;
            imgQrcode.Source = GetQRCode(url);

            //  btnCancel.Click += (s, e) => this.Close();
        }

        private Bitmap? GetQRCode(string? url)
        {
            var qrCodeImage = QRCodeHelper.GenQRCode(url);
            if (qrCodeImage is null) return null;
            var ms = new MemoryStream(qrCodeImage);
            return new Bitmap(ms);
        }
    }
}