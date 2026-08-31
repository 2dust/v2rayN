namespace ServiceLib.Models.Dto;

public record ClashItem
{
    public Dictionary<string, ClashProxy> Proxies { get; init; } = [];
    public Dictionary<string, string> ProviderIndexMap { get; init; } = [];

    public bool IsEmpty() => Proxies.Count == 0;
}

public record ClashProxy
{
    public List<string>? all { get; init; }
    public List<HistoryItem>? history { get; init; }
    public string? name { get; init; }
    public string? type { get; init; }
    public bool udp { get; init; }
    public string? now { get; init; }
    public int delay { get; init; }

    public record HistoryItem
    {
        public string? time { get; init; }
        public int delay { get; init; }
    }
}

public record ClashProvider
{
    public string? name { get; init; }
    public List<ClashProxy>? proxies { get; init; }
    public string? type { get; init; }
    public string? vehicleType { get; init; }
}

public record ClashProxies
{
    public Dictionary<string, ClashProxy> proxies { get; init; } = [];
}

public record ClashProviders
{
    public Dictionary<string, ClashProvider> providers { get; init; } = [];
}
