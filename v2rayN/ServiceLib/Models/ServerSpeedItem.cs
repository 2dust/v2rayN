namespace ServiceLib.Models;

[Serializable]
public class ServerSpeedItem : ServerStatItem
{
    public long ProxyUp { get; set; }

    public long ProxyDown { get; set; }

    public long DirectUp { get; set; }

    public long DirectDown { get; set; }
}

[Serializable]
public class TrafficItem
{
    public ulong Up { get; set; }

    public ulong Down { get; set; }
}
