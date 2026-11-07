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

    private static readonly string[] _allowedPhysicalNamePrefixes =
    [
        // Linux
        "eth", "enp", "ens", "eno", "enx", "wlp", "wlan",

        // macOS
        "en0", "en1", "en2",
    ];

    private static readonly string[] _allowedPhysicalNameKeywords =
    [
        // Windows / macOS / Generic
        "ethernet", "wlan", "wi-fi", "wifi", "airport",

        // Chinese
        "以太网", "无线", "局域网"
    ];

    private static readonly string[] _virtualNameBlacklistKeywords =
    [
        // Generic
        "virtual", "loopback", "loop back",

        // Common virtual / VPN / tunnel adapters
        "hyper-v", "vmware", "virtualbox", "vbox", "docker", "veth", "virbr", "cni",
        "flannel", "cali", "kube", "wintun", "wireguard", "zerotier", "tailscale", "utun",
        "tap-windows", "tap adapter", "openvpn", "anyconnect", "vpn", "clash", "mihomo", "xray_tun",

        // Windows NDIS filter sub-layers & pseudo-interfaces
        "wan miniport", "lightweight filter", "filter driver", "packet scheduler", "npcap", "kernel debug",

        // Chinese
        "虚拟", "回环", "环回"
    ];

    public static bool IsAutoBindInterface(string? bindInterface)
    {
        return string.Equals(bindInterface?.TrimEx(), Global.BindInterfaceAuto, StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> GetBindInterfaceNames()
    {
        return GetLocalNetworkInterfaceNames().Prepend(Global.BindInterfaceAuto).AppendEmpty();
    }

    public static string? GetRealBindInterfaceName(string? bindInterface)
    {
        var trimmed = bindInterface?.TrimEx();
        if (trimmed.IsNullOrEmpty())
        {
            return null;
        }

        return IsAutoBindInterface(trimmed) ? GetDefaultPhysicalInterfaceName() : trimmed;
    }

    /// <summary>
    /// Resolves a default physical outbound interface using a heuristic (active IP-enabled physical NICs
    /// with an IPv4 gateway, preferring Ethernet over Wi-Fi). This does not inspect OS routing metrics;
    /// in multi-NIC environments with custom routing tables, specify the target interface name manually.
    /// </summary>
    public static string? GetDefaultPhysicalInterfaceName()
    {
        try
        {
            var upPhysicalNics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsAllowedPhysicalInterface)
                .ToList();

            if (upPhysicalNics.Count == 0)
            {
                return null;
            }

            var gatewayNics = upPhysicalNics.Where(HasIPv4Gateway).ToList();
            var candidates = gatewayNics.Count > 0 ? gatewayNics : upPhysicalNics;

            var bestNic = candidates.FirstOrDefault(ni =>
                    ni.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet)
                ?? candidates.FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                ?? candidates.FirstOrDefault();

            return bestNic?.Name;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("GetDefaultPhysicalInterfaceName", ex);
            return null;
        }
    }

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
        catch (Exception ex)
        {
            Logging.SaveLog("GetLocalNetworkInterfaceNames", ex);
            return [];
        }
    }

    private static bool IsAllowedPhysicalInterface(NetworkInterface ni)
    {
        if (ni.OperationalStatus != OperationalStatus.Up || !SupportsIP(ni))
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

        if (_allowedPhysicalNamePrefixes.Any(prefix => ni.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return _allowedPhysicalNameKeywords.Any(keyword => interfaceText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SupportsIP(NetworkInterface ni)
    {
        try
        {
            return ni.Supports(NetworkInterfaceComponent.IPv4) || ni.Supports(NetworkInterfaceComponent.IPv6);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasIPv4Gateway(NetworkInterface ni)
    {
        try
        {
            return ni.GetIPProperties().GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.Any.Equals(g.Address)
                && !IPAddress.None.Equals(g.Address));
        }
        catch
        {
            return false;
        }
    }
}
