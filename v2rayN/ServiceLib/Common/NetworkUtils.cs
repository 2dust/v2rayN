using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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

    public static bool IsAuto(string? bindInterface)
    {
        return bindInterface?.TrimEx().Equals(Global.Auto, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public static List<string> GetBindInterfaceNames()
    {
        return GetLocalNetworkInterfaceNames().Prepend(Global.Auto).AppendEmpty();
    }

    public static string? GetRealBindInterfaceName(string? bindInterface)
    {
        var trimmed = bindInterface?.TrimEx();
        if (trimmed.IsNullOrEmpty())
        {
            return null;
        }

        if (IsAuto(trimmed))
        {
            return GetDefaultPhysicalInterfaceName();
        }

        return trimmed;
    }

    public static string? GetDefaultPhysicalInterfaceName()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Select(ToCandidate);
            return SelectDefaultInterface(candidates);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("GetDefaultPhysicalInterfaceName", ex);
            return null;
        }
    }

    public static string? SelectDefaultInterface(IEnumerable<NetworkInterfaceCandidate> candidates)
    {
        var upPhysicalNics = candidates
            .Where(IsAllowedPhysicalCandidate)
            .ToList();

        var gatewayNics = upPhysicalNics.Where(c => c.HasIPv4Gateway).ToList();
        var candidateList = gatewayNics.Count > 0 ? gatewayNics : upPhysicalNics;

        var bestNic = candidateList.FirstOrDefault(c =>
            c.InterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet)
            ?? candidateList.FirstOrDefault(c => c.InterfaceType == NetworkInterfaceType.Wireless80211)
            ?? candidateList.FirstOrDefault();

        return bestNic?.Name;
    }

    public static List<string> GetLocalNetworkInterfaceNames()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Select(ToCandidate)
                .Where(IsAllowedPhysicalCandidate)
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

    private static NetworkInterfaceCandidate ToCandidate(NetworkInterface ni)
    {
        return new NetworkInterfaceCandidate(
            ni.Name,
            ni.Description,
            ni.NetworkInterfaceType,
            ni.OperationalStatus,
            HasIPv4Gateway(ni));
    }

    public static bool IsAllowedPhysicalCandidate(NetworkInterfaceCandidate c)
    {
        if (c.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        var interfaceText = $"{c.Name} {c.Description}";
        if (_virtualNameBlacklistKeywords.Any(keyword => interfaceText.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (_allowedPhysicalInterfaceTypes.Contains(c.InterfaceType))
        {
            return true;
        }

        return _allowedPhysicalNameKeywords.Any(keyword => interfaceText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
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

public record NetworkInterfaceCandidate(
    string Name,
    string Description,
    NetworkInterfaceType InterfaceType,
    OperationalStatus OperationalStatus,
    bool HasIPv4Gateway);
