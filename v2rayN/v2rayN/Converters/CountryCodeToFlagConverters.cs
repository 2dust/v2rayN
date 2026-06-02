using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Flags.Icons;

namespace v2rayN.Converters;

/// <summary>
/// Converts a two-letter country code into a <see cref="LipisFlag"/> value for the
/// <c>Flags.Icons.WPF</c> FlagIcon control. Parsing is case-insensitive and returns
/// <see cref="LipisFlag.None"/> when the code is empty or not a recognized flag, so the
/// control renders nothing for unknown codes.
/// </summary>
public class CountryCodeToLipisFlagConverter : IValueConverter
{
    /// <summary>
    /// Resolves the <see cref="LipisFlag"/> for the given country code, or
    /// <see cref="LipisFlag.None"/> when it is empty or unrecognized.
    /// </summary>
    public static LipisFlag GetFlag(string? countryCode)
    {
        var normalizedCode = countryCode.NormalizeCountryCode();
        if (normalizedCode is not null && Enum.TryParse<LipisFlag>(normalizedCode, ignoreCase: true, out var flag))
        {
            return flag;
        }
        return LipisFlag.None;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return GetFlag(value?.ToString());
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

/// <summary>
/// Maps a country code to <see cref="Visibility"/>: visible only when it resolves to a real flag,
/// so the flag is hidden (without leaving an empty gap) for missing or unknown codes.
/// </summary>
public class CountryCodeToFlagVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> when the code resolves to a real flag, otherwise
    /// <see cref="Visibility.Collapsed"/> so no empty gap is left for missing or unknown codes.
    /// </summary>
    public static Visibility GetVisibility(string? countryCode)
    {
        return CountryCodeToLipisFlagConverter.GetFlag(countryCode) == LipisFlag.None
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return GetVisibility(value?.ToString());
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}
