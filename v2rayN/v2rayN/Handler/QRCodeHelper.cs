using System;
using System.Drawing;
using ZXing;
using ZXing.QrCode;

namespace v2rayN.Handler
{
    /// <summary>
    /// 含有QR码的描述类和包装编码和渲染
    /// </summary>
    public class QRCodeHelper
    {
        public static Image GetQRCode(string strContent)
        {
            Image img = null;
            try
            {
                QrCodeEncodingOptions options = new QrCodeEncodingOptions
                {
                    CharacterSet = "UTF-8",
                    DisableECI = true, // Extended Channel Interpretation (ECI) 主要用于特殊的字符集。并不是所有的扫描器都支持这种编码。
                    ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M, // 纠错级别
                    Width = 500,
                    Height = 500,
                    Margin = 1
                };
                // options.Hints，更多属性，也可以在这里添加。

                BarcodeWriter writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = options
                };
                Bitmap bmp = writer.Write(strContent);
                img = (Image)bmp;
                return img;
            }
            catch(Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return img;
            }
        }
    }
}
