using System.IO;
using Avalonia;
using Avalonia.Media;

namespace v2rayN.Desktop.Common;

public static class AppBuilderExtension
{
    public static AppBuilder WithFontByDefault(this AppBuilder appBuilder)
    {
        var notoSansSc = new FontFamily(Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC"));

        var fallbacks = new[]
        {
            new FontFallback { FontFamily = notoSansSc },

            OperatingSystem.IsLinux()
                ? new FontFallback { FontFamily = new FontFamily("Noto Color Emoji") }
                : null
        };

        var validFallbacks = fallbacks.Where(f => f is not null).ToArray()!;

        return appBuilder.With(new FontManagerOptions
        {
            FontFallbacks = validFallbacks
        });
    }
}
