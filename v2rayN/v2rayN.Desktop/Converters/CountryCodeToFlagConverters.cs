using System.Collections.Concurrent;
using Avalonia.Data.Converters;
using Avalonia.Platform;

namespace v2rayN.Desktop.Converters;

/// <summary>
/// Converts a two-letter country code into the Avalonia resource URI of the matching SVG flag,
/// consumed directly by the <c>Svg</c> control. Asset existence is cached per process because the
/// bundled resources cannot change at runtime, so each code is probed only once.
/// </summary>
public class CountryCodeToFlagPathConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, bool> FlagAvailability = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the <c>avares://</c> path of the flag for the given country code, or <c>null</c>
    /// when the code is invalid or no matching SVG asset is bundled.
    /// </summary>
    public static string? GetFlagPath(string? countryCode)
    {
        var normalizedCode = countryCode.NormalizeCountryCode();
        if (normalizedCode is null)
        {
            return null;
        }

        // The flags ship as embedded resources inside the Lipis.Flags.Avalonia package. Note that
        // this avares path targets the package's internal resource layout (Assets/4x3/<iso>.svg),
        // not a documented public API: a future package version that reorganizes its resources could
        // silently break flag lookup (AssetLoader.Exists returns false -> flag hidden) while the
        // version bump still succeeds. Revisit this path when upgrading the package.
        var uri = new Uri($"avares://Lipis.Flags.Avalonia/Assets/4x3/{normalizedCode.ToLowerInvariant()}.svg");
        // AssetLoader.Exists avoids handing the Svg control a path that would fail to load.
        return FlagAvailability.GetOrAdd(normalizedCode, _ => AssetLoader.Exists(uri)) ? uri.ToString() : null;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return GetFlagPath(value?.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}

/// <summary>
/// Maps a country code to a visibility flag: <c>true</c> only when a flag asset exists, so the
/// image is hidden (without leaving an empty gap) for missing or unknown codes.
/// </summary>
public class CountryCodeToFlagVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return CountryCodeToFlagPathConverter.GetFlagPath(value?.ToString()) is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
