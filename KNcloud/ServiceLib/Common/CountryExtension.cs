namespace ServiceLib.Common;

/// <summary>
/// Extension methods for country code utilities
/// </summary>
public static class CountryExtension
{
    /// <summary>
    /// Country code to emoji flag mapping for common countries
    /// </summary>
    private static readonly Dictionary<string, string> CountryEmojiMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Asia
        { "CN", "🇨🇳" }, // China
        { "HK", "🇭🇰" }, // Hong Kong
        { "TW", "🇹🇼" }, // Taiwan
        { "JP", "🇯🇵" }, // Japan
        { "SG", "🇸🇬" }, // Singapore
        { "KR", "🇰🇷" }, // South Korea
        { "TH", "🇹🇭" }, // Thailand
        { "VN", "🇻🇳" }, // Vietnam
        { "ID", "🇮🇩" }, // Indonesia
        { "PH", "🇵🇭" }, // Philippines
        { "MY", "🇲🇾" }, // Malaysia
        { "IN", "🇮🇳" }, // India
        { "PK", "🇵🇰" }, // Pakistan
        { "BD", "🇧🇩" }, // Bangladesh
        { "LK", "🇱🇰" }, // Sri Lanka
        { "KH", "🇰🇭" }, // Cambodia
        { "LA", "🇱🇦" }, // Laos
        { "MM", "🇲🇲" }, // Myanmar

        // Americas
        { "US", "🇺🇸" }, // United States
        { "CA", "🇨🇦" }, // Canada
        { "MX", "🇲🇽" }, // Mexico
        { "BR", "🇧🇷" }, // Brazil
        { "AR", "🇦🇷" }, // Argentina
        { "CL", "🇨🇱" }, // Chile
        { "CO", "🇨🇴" }, // Colombia

        // Europe
        { "GB", "🇬🇧" }, // United Kingdom
        { "DE", "🇩🇪" }, // Germany
        { "FR", "🇫🇷" }, // France
        { "IT", "🇮🇹" }, // Italy
        { "ES", "🇪🇸" }, // Spain
        { "RU", "🇷🇺" }, // Russia
        { "NL", "🇳🇱" }, // Netherlands
        { "CH", "🇨🇭" }, // Switzerland
        { "SE", "🇸🇪" }, // Sweden
        { "NO", "🇳🇴" }, // Norway
        { "DK", "🇩🇰" }, // Denmark
        { "FI", "🇫🇮" }, // Finland
        { "PL", "🇵🇱" }, // Poland
        { "CZ", "🇨🇿" }, // Czech Republic
        { "AT", "🇦🇹" }, // Austria
        { "GR", "🇬🇷" }, // Greece
        { "PT", "🇵🇹" }, // Portugal
        { "TR", "🇹🇷" }, // Turkey
        { "UA", "🇺🇦" }, // Ukraine
        { "RO", "🇷🇴" }, // Romania

        // Middle East & Central Asia
        { "AE", "🇦🇪" }, // United Arab Emirates
        { "SA", "🇸🇦" }, // Saudi Arabia
        { "IL", "🇮🇱" }, // Israel
        { "KZ", "🇰🇿" }, // Kazakhstan

        // Oceania
        { "AU", "🇦🇺" }, // Australia
        { "NZ", "🇳🇿" }, // New Zealand

        // Africa
        { "ZA", "🇿🇦" }, // South Africa
        { "EG", "🇪🇬" }, // Egypt
    };

    /// <summary>
    /// Converts country code to flag emoji using predefined mapping
    /// Example: "US" -> "🇺🇸", "CN" -> "🇨🇳"
    /// </summary>
    public static string? CountryToEmoji(this string? countryCode)
    {
        if (countryCode.IsNullOrEmpty())
        {
            return null;
        }

        return CountryEmojiMap.TryGetValue(countryCode, out var emoji) ? emoji : null;
    }
}
