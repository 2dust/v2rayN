namespace ServiceLib.Models.Dto;

public class SsSIP008
{
    public List<SsServer>? servers { get; set; }
}

public class SsServer
{
    public string? remarks { get; set; }
    public string? server { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int server_port { get; set; }
    public string? method { get; set; }
    public string? password { get; set; }
    public string? plugin { get; set; }
}
