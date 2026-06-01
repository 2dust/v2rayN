namespace ServiceLib.Models.Dto;

internal class IPAPIInfo
{
    public string? ip { get; set; }
    public string? clientIp { get; set; }
    public string? ip_addr { get; set; }
    public string? query { get; set; }
    public string? country { get; set; }
    public string? country_name { get; set; }
    public string? country_code { get; set; }
    public string? countryCode { get; set; }
    public LocationInfo? location { get; set; }
}

public class LocationInfo
{
    public string? country_code { get; set; }
}

public readonly record struct IpInfoResult(string Country, string? Ip)
{
    public string? CountryCode => Country.NormalizeCountryCode();

    public override string ToString()
    {
        // The country flag is rendered separately as an SVG image,
        // so the text contains only the country code and the IP address.
        return $"({CountryCode ?? Country}) {Ip}";
    }
}

/// <summary>
/// Result of an availability check: the ready-to-display string and the normalized two-letter
/// country code used for the flag (typically ISO 3166-1 alpha-2; <c>null</c> when unknown).
/// </summary>
public readonly record struct AvailabilityResult(string Message, string? CountryCode);
