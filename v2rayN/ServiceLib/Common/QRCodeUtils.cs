using QRCoder;
using QRCoder.Exceptions;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace ServiceLib.Common;

public class QRCodeUtils
{
    public static byte[]? GenQRCode(string? url)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }
        using QRCodeGenerator qrGenerator = new();
        DataTooLongException? lastDtle = null;

        var levels = new[]
        {
            QRCodeGenerator.ECCLevel.H,
            QRCodeGenerator.ECCLevel.Q,
            QRCodeGenerator.ECCLevel.M,
            QRCodeGenerator.ECCLevel.L
        };
        foreach (var level in levels)
        {
            try
            {
                using var qrCodeData = qrGenerator.CreateQrCode(url, level);
                using PngByteQRCode qrCode = new(qrCodeData);
                return qrCode.GetGraphic(20);
            }
            catch (DataTooLongException ex)
            {
                lastDtle = ex;
                continue;
            }
            catch
            {
                throw;
            }
        }

        if (lastDtle != null)
        {
            throw lastDtle;
        }

        return null;
    }

    public static string? ParseBarcode(string? fileName)
    {
        if (fileName == null || !File.Exists(fileName))
        {
            return null;
        }

        try
        {
            using var data = SKData.Create(fileName);
            var bitmap = DecodeWithinLimit(data);

            return ReaderBarcode(bitmap);
        }
        catch
        {
            // ignored
        }

        return null;
    }

    // A QR code from a screen capture or an image file is at most a few megapixels. A crafted
    // image header can declare enormous dimensions (e.g. a 26-byte GIF claiming 4097x65529 =
    // ~268 MP), making SKBitmap.Decode allocate gigabytes; the reader then scans a flipped copy
    // too, so the process hangs on ~8s of CPU and multiple GB of RAM. Cap the pixel count well
    // above any real QR before decoding.
    private const long MaxBarcodeImagePixels = 4096L * 4096L;

    private static SKBitmap? DecodeWithinLimit(SKData data)
    {
        if (data == null)
        {
            return null;
        }
        using var codec = SKCodec.Create(data);
        if (codec == null)
        {
            return null;
        }
        var info = codec.Info;
        if ((long)info.Width * info.Height > MaxBarcodeImagePixels)
        {
            return null;
        }
        return SKBitmap.Decode(codec);
    }

    public static string? ParseBarcode(byte[]? bytes)
    {
        if (bytes == null)
        {
            return null;
        }
        try
        {
            using var data = SKData.CreateCopy(bytes);
            var bitmap = DecodeWithinLimit(data);
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
        if (bitmap == null)
        {
            return null;
        }
        var reader = new BarcodeReader();
        var result = reader.Decode(bitmap);

        if (result != null && result.Text.IsNotEmpty())
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
