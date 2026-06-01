using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace v2rayN.Converters;

/// <summary>
/// Converts a two-letter country code into a cached <see cref="DrawingImage"/> of the matching SVG
/// flag bundled as a WPF resource. A standard <see cref="System.Windows.Controls.Image"/> renders
/// the result: the third-party <c>SVGImage</c> control did not reliably draw the vector image in a
/// single-file build, so the SVG is rasterized to a <see cref="Drawing"/> once and reused.
/// </summary>
public class CountryCodeToFlagDrawingConverter : IValueConverter
{
    // Successful lookups: normalized code -> frozen DrawingImage (safe to share across threads).
    private static readonly ConcurrentDictionary<string, DrawingImage> FlagImages = new(StringComparer.OrdinalIgnoreCase);

    // Negative cache: codes with no bundled asset, so we probe the resource only once per code.
    private static readonly ConcurrentDictionary<string, byte> MissingFlags = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the flag image for the given country code, or <c>null</c> when the code is invalid
    /// or no matching SVG asset is bundled.
    /// </summary>
    public static DrawingImage? GetFlagImage(string? countryCode)
    {
        var normalizedCode = countryCode.NormalizeCountryCode();
        if (normalizedCode is null)
        {
            return null;
        }

        if (FlagImages.TryGetValue(normalizedCode, out var cachedImage))
        {
            return cachedImage;
        }
        if (MissingFlags.ContainsKey(normalizedCode))
        {
            return null;
        }

        // The "v2rayN;component" form makes the pack URI resolve reliably from this assembly,
        // including in a single-file published build.
        var uri = new Uri($"pack://application:,,,/v2rayN;component/Resources/Flags/{normalizedCode.ToLowerInvariant()}.svg", UriKind.Absolute);
        try
        {
            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is null)
            {
                MissingFlags.TryAdd(normalizedCode, 0);
                return null;
            }

            // Rasterize the SVG into a WPF Drawing, then freeze it so it can be cached and shared.
            var drawing = new SVGImage.SVG.SVGRender().LoadDrawing(stream);
            var image = new DrawingImage(drawing);
            if (image.CanFreeze)
            {
                image.Freeze();
            }
            return FlagImages.GetOrAdd(normalizedCode, image);
        }
        catch
        {
            MissingFlags.TryAdd(normalizedCode, 0);
            return null;
        }
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return GetFlagImage(value?.ToString());
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

/// <summary>
/// Maps a country code to <see cref="Visibility"/>: visible only when a flag asset exists, so the
/// image is hidden (without leaving an empty gap) for missing or unknown codes.
/// </summary>
public class CountryCodeToFlagVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return CountryCodeToFlagDrawingConverter.GetFlagImage(value?.ToString()) is null
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}
