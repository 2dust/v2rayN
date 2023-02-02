using System.IO;
using System.Windows.Media;
using v2rayN.Handler;

namespace v2rayN.Converters
{
    public class MaterialDesignFonts
    {
        public static FontFamily MyFont { get; }

        static MaterialDesignFonts()
        {
            try
            {
                var fontFamily = LazyConfig.Instance.GetConfig().uiItem.currentFontFamily;
                if (!string.IsNullOrEmpty(fontFamily))
                {
                    var fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Fonts\");
                    MyFont = new FontFamily(new Uri($"file:///{fontPath}"), $"./#{fontFamily}");
                }
            }
            catch
            {
            }
            if (MyFont is null)
            {
                MyFont = new FontFamily("Microsoft YaHei");
            }
        }
    }
}