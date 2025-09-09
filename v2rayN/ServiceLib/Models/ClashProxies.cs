namespace ServiceLib.Models;

public class ClashProxies
{
    public Dictionary<string, ProxiesItem>? proxies { get; set; }

    public class ProxiesItem
    {
        public List<string>? all { get; set; }
        public List<HistoryItem>? history { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public bool udp { get; set; }
        public string? now { get; set; }
        public int delay { get; set; }
    }

    public class HistoryItem
    {
        public string? time { get; set; }
        public int delay { get; set; }
    }
}
