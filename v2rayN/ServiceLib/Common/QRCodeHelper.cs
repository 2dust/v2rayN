using QRCoder;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace ServiceLib.Common
{
    public class QRCodeHelper
    {
        public static byte[]? GenQRCode(string? url)
        {
            using QRCodeGenerator qrGenerator = new();
            using var qrCodeData = qrGenerator.CreateQrCode(url ?? string.Empty, QRCodeGenerator.ECCLevel.Q);
            using PngByteQRCode qrCode = new(qrCodeData);
            return qrCode.GetGraphic(20);
        }

        public static string? ParseBarcode(string? fileName)
        {
            if (fileName == null || !File.Exists(fileName))
            {
                return null;
            }

            try
            {
                var image = SKImage.FromEncodedData(fileName);
                var bitmap = SKBitmap.FromImage(image);

                return ReaderBarcode(bitmap);
            }
            catch
            {
                // ignored
            }

            return null;
        }

        public static string? ParseBarcode(byte[]? bytes)
        {
            try
            {
                var bitmap = SKBitmap.Decode(bytes);
                //using var stream = new FileStream("test2.png", FileMode.Create, FileAccess.Write);
                //using var image = SKImage.FromBitmap(bitmap);
                //using var encodedImage = image.Encode();
                //encodedImage.SaveTo(stream);
                return ReaderBarcode(bitmap);
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static string? ReaderBarcode(SKBitmap? bitmap)
        {
            var reader = new BarcodeReader();
            var result = reader.Decode(bitmap);

            if (result != null && Utils.IsNotEmpty(result.Text))
            {
                return result.Text;
            }

            //FlipBitmap
            var result2 = reader.Decode(FlipBitmap(bitmap));
            return result2?.Text;
        }

        private static SKBitmap FlipBitmap(SKBitmap bmp)
        {
            // Create a bitmap (to return)
            var flipped = new SKBitmap(bmp.Width, bmp.Height, bmp.Info.ColorType, bmp.Info.AlphaType);

            // Create a canvas to draw into the bitmap
            using var canvas = new SKCanvas(flipped);

            // Set a transform matrix which moves the bitmap to the right,
            // and then "scales" it by -1, which just flips the pixels
            // horizontally
            canvas.Translate(bmp.Width, 0);
            canvas.Scale(-1, 1);
            canvas.DrawBitmap(bmp, 0, 0);
            return flipped;
        }
    }
}