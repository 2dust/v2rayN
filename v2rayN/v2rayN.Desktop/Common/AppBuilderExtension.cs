using Avalonia;
using Avalonia.Media;
using System.Reflection;

namespace v2rayN.Desktop.Common
{
    public static class AppBuilderExtension
    {
        public static AppBuilder WithFontByDefault(this AppBuilder appBuilder)
        {
            var uri = $"avares://{Assembly.GetExecutingAssembly().GetName().Name}/Assets/Fonts#Noto Sans SC";
            return appBuilder.With(new FontManagerOptions()
            {
                DefaultFamilyName = uri,
                FontFallbacks = new[] { new FontFallback { FontFamily = new FontFamily(uri) } }
            });
        }
    }
}