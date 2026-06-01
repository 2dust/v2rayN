namespace ServiceLib.Common;

/// <summary>
/// Helpers for working with two-letter country codes (typically ISO 3166-1 alpha-2).
/// </summary>
public static class CountryExtension
{
    /// <summary>
    /// Normalizes a value to an upper-case two-letter country code. Returns the normalized code
    /// (for example "DE") for any two ASCII letters, or <c>null</c> for anything else (empty,
    /// "unknown", non-letter characters, wrong length). Note: this is a shape check only; it does
    /// not verify the code against the official ISO 3166-1 list (for example "ZZ" is accepted).
    /// </summary>
    public static string? NormalizeCountryCode(this string? value)
    {
        return value is { Length: 2 } && char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1])
            ? value.ToUpperInvariant()
            : null;
    }

    /// <summary>
    /// Extracts a normalized country code from a stored IP information string and removes the
    /// legacy regional-indicator emoji prefix when present.
    /// </summary>
    public static string NormalizeStoredIpInfo(this string? value, out string? countryCode)
    {
        countryCode = null;
        if (value.IsNullOrEmpty())
        {
            return value ?? string.Empty;
        }

        var openingParenthesis = value.IndexOf('(');
        if (openingParenthesis is not (0 or 4)
            || value.Length < openingParenthesis + 4
            || value[openingParenthesis + 3] != ')')
        {
            return value;
        }

        if (openingParenthesis == 4 && !IsRegionalIndicatorFlag(value[..4]))
        {
            return value;
        }

        countryCode = value.Substring(openingParenthesis + 1, 2).NormalizeCountryCode();
        return countryCode is null ? value : value[openingParenthesis..];
    }

    private static bool IsRegionalIndicatorFlag(string value)
    {
        return value.Length == 4
            && char.ConvertToUtf32(value, 0) is >= 0x1F1E6 and <= 0x1F1FF
            && char.ConvertToUtf32(value, 2) is >= 0x1F1E6 and <= 0x1F1FF;
    }
}
