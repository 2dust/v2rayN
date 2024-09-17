using System.Windows.Media;

namespace v2rayN.Converters
{
    public class MaterialDesignFonts
    {
        public static FontFamily MyFont { get; }

        static MaterialDesignFonts()
        {
            try
            {
                var fontFamily = LazyConfig.Instance.Config.uiItem.currentFontFamily;
                if (Utils.IsNotEmpty(fontFamily))
                {
                    var fontPath = Utils.GetFontsPath();
                    MyFont = new FontFamily(new Uri(@$"file:///{fontPath}\"), $"./#{fontFamily}");
                }
            }
            catch
            {
            }
            MyFont ??= new FontFamily("Microsoft YaHei");
        }
    }
}