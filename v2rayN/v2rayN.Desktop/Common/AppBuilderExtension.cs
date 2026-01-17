namespace v2rayN.Desktop.Common;

public static class AppBuilderExtension
{
    public static AppBuilder WithFontByDefault(this AppBuilder appBuilder)
    {
        var fallbacks = new List<FontFallback>();

        var notoSansSc = new FontFamily(Path.Combine(AppConfig.AvaAssets, "Fonts#Noto Sans SC"));
        fallbacks.Add(new FontFallback { FontFamily = notoSansSc });

        if (OperatingSystem.IsLinux())
        {
            fallbacks.Add(new FontFallback
            {
                FontFamily = new FontFamily("Noto Color Emoji")
            });
        }

        return appBuilder.With(new FontManagerOptions
        {
            FontFallbacks = fallbacks.ToArray()
        });
    }
}
