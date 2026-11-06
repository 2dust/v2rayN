namespace ServiceLib.Common;

public static class NetworkUtils
{
    private static readonly HashSet<NetworkInterfaceType> _allowedPhysicalInterfaceTypes =
    [
        NetworkInterfaceType.Ethernet,
        NetworkInterfaceType.Wireless80211,
        NetworkInterfaceType.GigabitEthernet,
        NetworkInterfaceType.FastEthernetFx,
        NetworkInterfaceType.FastEthernetT,
    ];

    private static readonly string[] _allowedPhysicalNameKeywords =
    [
        // Windows / Generic
        "ethernet", "wlan", "wi-fi", "wifi",

        // Linux
        "eth", "enp", "ens", "eno", "enx", "wlp", "wlan",

        // macOS
        "en0", "en1", "en2", "airport",

        // Chinese
        "以太网", "无线", "局域网"
    ];

    private static readonly string[] _virtualNameBlacklistKeywords =
    [
        // Generic
        "virtual", "loopback", "loop back",

        // Common virtual adapters
        "hyper-v", "vmware", "virtualbox", "vbox", "docker", "veth", "virbr", "cni",
        "flannel", "cali", "kube", "wintun", "wireguard", "zerotier", "tailscale", "utun",

        // Chinese
        "虚拟", "回环", "环回"
    ];

    public static List<string> GetLocalNetworkInterfaceNames()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsAllowedPhysicalInterface)
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsAllowedPhysicalInterface(NetworkInterface ni)
    {
        if (ni.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        var interfaceText = $"{ni.Name} {ni.Description}";
        if (_virtualNameBlacklistKeywords.Any(keyword => interfaceText.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (_allowedPhysicalInterfaceTypes.Contains(ni.NetworkInterfaceType))
        {
            return true;
        }

        return _allowedPhysicalNameKeywords.Any(keyword => interfaceText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
