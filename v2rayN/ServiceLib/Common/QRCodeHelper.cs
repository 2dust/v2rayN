using QRCoder;

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
    }
}