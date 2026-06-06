namespace v2rayN.Desktop.Common;

public static class AppBuilderExtension
{
    private static readonly string DefaultFontFamilyName =
        Path.Combine(Global.AvaAssets, "Fonts#Noto Sans SC");

    public static AppBuilder WithFontByDefault(this AppBuilder appBuilder)
    {
        var fallbacks = new List<FontFallback>();

        var notoSansSc = new FontFamily(DefaultFontFamilyName);

        fallbacks.Add(new FontFallback
        {
            FontFamily = notoSansSc
        });

        if (OperatingSystem.IsWindows())
        {
            AddFontFallback(fallbacks, "Segoe UI Symbol");
            AddFontFallback(fallbacks, "Segoe UI Emoji");
        }
        else if (OperatingSystem.IsMacOS())
        {
            AddFontFallback(fallbacks, "Apple Symbols");
            AddFontFallback(fallbacks, "Apple Color Emoji");
        }
        else if (OperatingSystem.IsLinux())
        {
            AddFontFallback(fallbacks, "Noto Sans Symbols");
            AddFontFallback(fallbacks, "Noto Color Emoji");
        }

        return appBuilder.With(new FontManagerOptions
        {
            DefaultFamilyName = DefaultFontFamilyName,
            FontFallbacks = fallbacks.ToArray()
        });
    }

    private static void AddFontFallback(List<FontFallback> fallbacks, string fontFamilyName)
    {
        fallbacks.Add(new FontFallback
        {
            FontFamily = new FontFamily(fontFamilyName)
        });
    }
}
