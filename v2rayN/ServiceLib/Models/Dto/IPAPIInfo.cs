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
    public string? region { get; set; }
    public string? region_name { get; set; }
    public string? regionName { get; set; }
    public string? city { get; set; }
    public LocationInfo? location { get; set; }
}

public class LocationInfo
{
    public string? country { get; set; }
    public string? country_code { get; set; }
    public string? region { get; set; }
    public string? region_name { get; set; }
    public string? state { get; set; }
    public string? city { get; set; }
}

public readonly record struct IpInfoResult(string Country, string? Ip, string? Region = null, string? City = null)
{
    public override string ToString()
    {
        var emoji = Utils.IsWindows() ? null : Country.CountryToEmoji();
        var country = emoji.IsNotEmpty() ? $"{emoji}({Country})" : Country;
        var location = string.Join(" ", DistinctNonEmpty(country, Region, City));

        return string.Join(" ", DistinctNonEmpty(location, Ip));
    }

    private static IEnumerable<string> DistinctNonEmpty(params string?[] values)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var trimmed = value?.Trim();
            if (trimmed.IsNullOrEmpty())
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}
