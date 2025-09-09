using static ServiceLib.Models.ClashProxies;

namespace ServiceLib.Models;

public class ClashProviders
{
    public Dictionary<string, ProvidersItem>? providers { get; set; }

    public class ProvidersItem
    {
        public string? name { get; set; }
        public List<ProxiesItem>? proxies { get; set; }
        public string? type { get; set; }
        public string? vehicleType { get; set; }
    }
}
