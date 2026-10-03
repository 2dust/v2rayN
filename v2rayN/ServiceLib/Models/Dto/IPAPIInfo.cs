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
    public override string ToString()
    {
        var emoji = Utils.IsWindows() ? null : Country.CountryToEmoji();
        return $"{emoji}({Country}) {Ip}";
    }
}
