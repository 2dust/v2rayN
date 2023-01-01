using QRCoder;
using QRCoder.Xaml;
using System.Windows.Media;

namespace v2rayN.Handler
{
    /// <summary>
    /// 含有QR码的描述类和包装编码和渲染
    /// </summary>
    public class QRCodeHelper
    {
        public static DrawingImage GetQRCode(string strContent)
        {
            try
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(strContent, QRCodeGenerator.ECCLevel.H);
                XamlQRCode qrCode = new XamlQRCode(qrCodeData);
                DrawingImage qrCodeAsXaml = qrCode.GetGraphic(40);
                return qrCodeAsXaml;
            }
            catch
            {
                return null;
            }
        }


    }
}
