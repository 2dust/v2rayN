using System.IO;
using System.Windows.Media;

namespace v2rayN.Converters
{
    public class MaterialDesignFonts
    {
        public static FontFamily MyFont { get; }

        static MaterialDesignFonts()
        {
            var fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Fonts\");
            MyFont = new FontFamily(new Uri($"file:///{fontPath}"), "./#Source Han Sans CN");
        }
    }
}