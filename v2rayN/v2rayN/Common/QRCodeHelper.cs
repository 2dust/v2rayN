using System.Collections;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace v2rayN
{
    public class QRCodeHelper
    {
        public static ImageSource? GetQRCode(string? strContent)
        {
            if (strContent is null)
            {
                return null;
            }
            try
            {
                var qrCodeImage = ServiceLib.Common.QRCodeHelper.GenQRCode(strContent);
                return qrCodeImage is null ? null : ByteToImage(qrCodeImage);
            }
            catch
            {
                return null;
            }
        }

        public static byte[]? CaptureScreen(Window window)
        {
            try
            {
                GetDpi(window, out var dpiX, out var dpiY);

                var left = (int)(SystemParameters.WorkArea.Left);
                var top = (int)(SystemParameters.WorkArea.Top);
                var width = (int)(SystemParameters.WorkArea.Width / dpiX);
                var height = (int)(SystemParameters.WorkArea.Height / dpiY);

                using var fullImage = new Bitmap(width, height);
                using var g = Graphics.FromImage(fullImage);

                g.CopyFromScreen(left, top, 0, 0, fullImage.Size, CopyPixelOperation.SourceCopy);
                //fullImage.Save("test1.png", ImageFormat.Png);
                return ImageToByte(fullImage);
            }
            catch
            {
                return null;
            }
        }

        private static void GetDpi(Window window, out float x, out float y)
        {
            var hWnd = new WindowInteropHelper(window).EnsureHandle();
            var g = Graphics.FromHwnd(hWnd);

            x = 96 / g.DpiX;
            y = 96 / g.DpiY;
        }

        private static ImageSource? ByteToImage(IEnumerable imageData)
        {
            return new ImageSourceConverter().ConvertFrom(imageData) as BitmapSource;
        }

        private static byte[]? ImageToByte(Image img)
        {
            return new ImageConverter().ConvertTo(img, typeof(byte[])) as byte[];
        }
    }
}