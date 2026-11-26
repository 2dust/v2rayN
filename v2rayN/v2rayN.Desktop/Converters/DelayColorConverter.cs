using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Converters;

/// <summary>
/// Colors the latency value with the semantic colors of the active Semi theme
/// (SemiColorSuccess / SemiColorWarning / SemiColorDanger), so it stays readable
/// on light, dark and the colored theme variants instead of using hardcoded colors.
///
/// The brushes are shared instances that are repainted when the theme variant changes,
/// which makes theme switching update the already rendered cells immediately.
///
/// The semantic colors of the Semi palette are tuned for filled surfaces, not for text:
/// SemiColorSuccess reaches only ~2.7:1 against the white row background of the light
/// theme variants, which is too low for a value that is read as text. The token color
/// is therefore darkened on light themes until it clears the WCAG AA contrast ratio
/// (4.5:1) for body text. On the dark variants the token colors already reach 5.7:1 to
/// 8.4:1 and are used unchanged.
/// </summary>
public class DelayColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Danger = new(Colors.IndianRed);
    private static readonly SolidColorBrush Success = new(Colors.Green);
    private static readonly SolidColorBrush Warning = new(Colors.Goldenrod);
    private static bool _subscribed;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        EnsureSubscribed();

        var delay = value?.ToString().ToInt() ?? 0;

        return delay switch
        {
            <= 0 => Danger,
            <= 300 => Success,
            <= 800 => Warning,
            _ => Danger
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;

    private static void EnsureSubscribed()
    {
        if (_subscribed || Application.Current is not { } app)
        {
            return;
        }

        _subscribed = true;
        UpdateBrushes();
        app.ActualThemeVariantChanged += (_, _) => UpdateBrushes();
    }

    private static void UpdateBrushes()
    {
        var isDark = ThemeHelper.IsDarkTheme();

        Apply("SemiColorDanger", Danger, isDark);
        Apply("SemiColorSuccess", Success, isDark);
        Apply("SemiColorWarning", Warning, isDark);
    }

    /// <summary>
    /// Reads a semantic color from the current theme and adjusts it, if needed, so that
    /// it stays legible as text on the table row background: the color is darkened on
    /// light themes and lightened on dark themes until the WCAG AA contrast ratio
    /// (4.5:1) is met.
    /// </summary>
    private static void Apply(string key, SolidColorBrush brush, bool isDark)
    {
        if (Application.Current is not { } app
            || !app.TryGetResource(key, app.ActualThemeVariant, out var value)
            || value is not ISolidColorBrush themed)
        {
            return;
        }

        var color = themed.Color;
        var rowBackground = ThemeHelper.GetRowBackground();
        color = isDark
            ? LightenUntilContrast(color, rowBackground)
            : DarkenUntilContrast(color, rowBackground);

        if (color != brush.Color)
        {
            brush.Color = color;
        }
    }

    /// <summary>
    /// Scales the color down until it contrasts sufficiently with the given background.
    /// </summary>
    private static Color DarkenUntilContrast(Color color, Color background)
    {
        var factor = 1.0;
        while (factor > 0.25)
        {
            var candidate = Color.FromRgb(
                (byte)Math.Clamp(color.R * factor, 0, 255),
                (byte)Math.Clamp(color.G * factor, 0, 255),
                (byte)Math.Clamp(color.B * factor, 0, 255));

            if (ContrastRatio(candidate, background) >= 4.5)
            {
                return candidate;
            }

            factor -= 0.05;
        }

        return color;
    }

    /// <summary>
    /// Scales the color up until it contrasts sufficiently with the given background.
    /// </summary>
    private static Color LightenUntilContrast(Color color, Color background)
    {
        var factor = 1.0;
        while (factor < 2.0)
        {
            var candidate = Color.FromRgb(
                (byte)Math.Clamp(color.R * factor, 0, 255),
                (byte)Math.Clamp(color.G * factor, 0, 255),
                (byte)Math.Clamp(color.B * factor, 0, 255));

            if (ContrastRatio(candidate, background) >= 4.5)
            {
                return candidate;
            }

            factor += 0.05;
        }

        return color;
    }

    /// <summary>
    /// WCAG relative luminance.
    ///
    /// The sRGB channels have to be linearized first, otherwise bright colors are
    /// massively overestimated: a plain weighted average reports #5DC264 as 0.65
    /// instead of its true 0.42, which would push the semantic colors far beyond
    /// the contrast ratio they already meet.
    /// </summary>
    private static double Luminance(Color color)
    {
        return 0.2126 * Linearize(color.R)
             + 0.7152 * Linearize(color.G)
             + 0.0722 * Linearize(color.B);
    }

    /// <summary>
    /// Converts one sRGB channel to its linear representation (WCAG 2.x).
    /// </summary>
    private static double Linearize(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045
            ? c / 12.92
            : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// WCAG contrast ratio between two colors.
    /// </summary>
    private static double ContrastRatio(Color a, Color b)
    {
        var l1 = Luminance(a);
        var l2 = Luminance(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

}
