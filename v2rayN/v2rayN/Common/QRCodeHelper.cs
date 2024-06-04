using QRCoder;
using QRCoder.Xaml;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace v2rayN
{
    /// <summary>
    /// 含有QR码的描述类和包装编码和渲染
    /// </summary>
    public class QRCodeHelper
    {
        public static DrawingImage? GetQRCode(string? strContent)
        {
            if (strContent is null)
            {
                return null;
            }
            try
            {
                QRCodeGenerator qrGenerator = new();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(strContent, QRCodeGenerator.ECCLevel.H);
                XamlQRCode qrCode = new(qrCodeData);
                DrawingImage qrCodeAsXaml = qrCode.GetGraphic(40);
                return qrCodeAsXaml;
            }
            catch
            {
                return null;
            }
        }

        public static string ScanScreen(float dpiX, float dpiY)
        {
            try
            {
                var left = (int)(SystemParameters.WorkArea.Left);
                var top = (int)(SystemParameters.WorkArea.Top);
                var width = (int)(SystemParameters.WorkArea.Width / dpiX);
                var height = (int)(SystemParameters.WorkArea.Height / dpiY);

                using Bitmap fullImage = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(fullImage))
                {
                    g.CopyFromScreen(left, top, 0, 0, fullImage.Size, CopyPixelOperation.SourceCopy);
                }
                int maxTry = 10;
                for (int i = 0; i < maxTry; i++)
                {
                    int marginLeft = (int)((double)fullImage.Width * i / 2.5 / maxTry);
                    int marginTop = (int)((double)fullImage.Height * i / 2.5 / maxTry);
                    Rectangle cropRect = new(marginLeft, marginTop, fullImage.Width - marginLeft * 2, fullImage.Height - marginTop * 2);
                    Bitmap target = new(width, height);

                    double imageScale = (double)width / (double)cropRect.Width;
                    using (Graphics g = Graphics.FromImage(target))
                    {
                        g.DrawImage(fullImage, new Rectangle(0, 0, target.Width, target.Height),
                                        cropRect,
                                        GraphicsUnit.Pixel);
                    }

                    BitmapLuminanceSource source = new(target);
                    QRCodeReader reader = new();

                    BinaryBitmap bitmap = new(new HybridBinarizer(source));
                    var result = reader.decode(bitmap);
                    if (result != null)
                    {
                        return result.Text;
                    }
                    else
                    {
                        BinaryBitmap bitmap2 = new(new HybridBinarizer(source.invert()));
                        var result2 = reader.decode(bitmap2);
                        if (result2 != null)
                        {
                            return result2.Text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return string.Empty;
        }

        public static Tuple<float, float> GetDpiXY(Window window)
        {
            IntPtr hWnd = new WindowInteropHelper(window).EnsureHandle();
            Graphics g = Graphics.FromHwnd(hWnd);

            return new(96 / g.DpiX, 96 / g.DpiY);
        }
    }
}