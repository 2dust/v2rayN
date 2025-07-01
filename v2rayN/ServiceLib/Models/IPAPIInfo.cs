namespace ServiceLib.Models;

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
