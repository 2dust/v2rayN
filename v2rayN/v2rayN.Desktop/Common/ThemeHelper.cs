using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace v2rayN.Desktop.Common;

/// <summary>
/// Helpers to read the colors of the active Semi theme variant at runtime.
///
/// The colored theme variants (Aquatic, Desert, Dusk, NightSky) are neither
/// "Light" nor "Dark" for Avalonia, so anything that needs to know whether it is
/// looking at a dark surface has to ask the theme palette instead of the variant
/// name.
/// </summary>
internal static class ThemeHelper
{
    /// <summary>
    /// Background tokens, ordered from the page background to the raised surfaces.
    ///
    /// SemiColorFill0 is deliberately not part of this list: it is an inverse token
    /// (white on the dark variants, near black on the light ones) and therefore says
    /// nothing about whether the active theme is dark.
    /// </summary>
    private static readonly string[] BackgroundKeys = ["SemiColorBackground0", "SemiColorBackground1"];

    /// <summary>
    /// Decides whether the active theme is a dark one by the luminance of its
    /// background, which classifies the colored variants correctly.
    /// </summary>
    public static bool IsDarkTheme()
    {
        if (Application.Current is not { } app)
        {
            return true;
        }

        foreach (var key in BackgroundKeys)
        {
            if (app.TryGetResource(key, app.ActualThemeVariant, out var value)
                && value is ISolidColorBrush brush)
            {
                return Luminance(brush.Color) < 0.5;
            }
        }

        return app.ActualThemeVariant != ThemeVariant.Light;
    }

    /// <summary>
    /// The color the table rows are drawn on in the active theme. It is used as the
    /// reference for contrast calculations of text that is drawn on top of them.
    /// </summary>
    public static Color GetRowBackground()
    {
        if (Application.Current is { } app
            && app.TryGetResource("SemiColorBackground1", app.ActualThemeVariant, out var value)
            && value is ISolidColorBrush brush)
        {
            return brush.Color;
        }

        return Colors.White;
    }

    private static double Luminance(Color color) => (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
}
