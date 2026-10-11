namespace ServiceLib.Models.Entities;

public class ProfileExItem
{
    [Key]
    public string IndexId { get; set; }

    public int Delay { get; set; }
    public decimal Speed { get; set; }
    public int Sort { get; set; }
    public string? Message { get; set; }
    public string? IpInfo { get; set; }
}
