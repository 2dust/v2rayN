namespace ServiceLib.Models.Entities;

public class ServerStatItem
{
    [Key]
    public string IndexId { get; set; }

    public long TotalUp { get; set; }

    public long TotalDown { get; set; }

    public long TodayUp { get; set; }

    public long TodayDown { get; set; }

    public long DateNow { get; set; }
}
