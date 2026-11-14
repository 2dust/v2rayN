using SkiaSharp;

namespace ServiceLib.Tests.Common;

public class QRCodeUtilsTests
{
    private const string ShareUri = "vless://12345678-1234-1234-1234-123456789abc@example.invalid:443?security=tls#native-qr-test";

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task ParseBarcode_ImageBytes_ShouldRecoverShareUri(bool mirrored, bool rotated)
    {
        var bytes = QRCodeUtils.GenQRCode(ShareUri);
        if (mirrored || rotated)
        {
            bytes = TransformImage(bytes!, mirrored, rotated);
        }

        await QRCodeUtils.ParseBarcode(bytes).Should().BeEqualTo(ShareUri);
    }

    [Test]
    public async Task ParseBarcode_ImageFile_ShouldRecoverShareUri()
    {
        var path = Path.Combine(Path.GetTempPath(), $"v2rayn-qr-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(path, QRCodeUtils.GenQRCode(ShareUri)!);

            await QRCodeUtils.ParseBarcode(path).Should().BeEqualTo(ShareUri);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] TransformImage(byte[] bytes, bool mirrored, bool rotated)
    {
        using var image = SKImage.FromEncodedData(bytes);
        using var surface = SKSurface.Create(new SKImageInfo(image.Width, image.Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        if (mirrored)
        {
            canvas.Translate(image.Width, 0);
            canvas.Scale(-1, 1);
        }
        if (rotated)
        {
            canvas.Translate(image.Width / 2f, image.Height / 2f);
            canvas.RotateDegrees(90);
            canvas.Translate(-image.Width / 2f, -image.Height / 2f);
        }
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        using var transformed = surface.Snapshot();
        using var encoded = transformed.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
