namespace ServiceLib.Services.AppRouting;

public sealed record AppRouteProcess(int Pid, string Name, string? Path, int TcpConnections, int UdpEndpoints)
{
    public string DisplayName => $"{Name} ({Pid})";
    public string DisplayConnections => $"{TcpConnections}/{UdpEndpoints}";
    public int TotalConnections => TcpConnections + UdpEndpoints;
    public string DisplayPath => Path ?? ResUI.AppRoutingPathUnavailable;

    public bool MatchesSearch(string search) => Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (Path?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) || Pid.ToString().Contains(search, StringComparison.Ordinal);
}

internal static class AppRouteProcessCatalog
{
    [SupportedOSPlatform("windows")]
    public static List<AppRouteProcess> Read()
    {
        var owners = new List<(int Pid, byte Protocol)>();
        foreach (var family in new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 })
        {
            if (family == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
            {
                continue;
            }

            foreach (var protocol in new byte[] { 6, 17 })
            {
                owners.AddRange(RouteOwnerTable.Read(protocol, family).Select(r => (r.Pid, protocol)));
            }
        }
        return Build(owners, ReadIdentity);
    }

    internal static List<AppRouteProcess> Build(IEnumerable<(int Pid, byte Protocol)> owners,
        Func<int, (string Name, string? Path)?> resolve, string? windowsDirectory = null)
    {
        windowsDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var result = new List<AppRouteProcess>();
        foreach (var group in owners.Where(o => o.Pid > 4).GroupBy(o => o.Pid))
        {
            var identity = resolve(group.Key);
            if (identity == null || AppRoutingManager.IsProtectedExecutable(identity.Value.Name) ||
                IsSystemExecutable(identity.Value.Name, identity.Value.Path, windowsDirectory))
            {
                continue;
            }

            result.Add(new(group.Key, identity.Value.Name, identity.Value.Path,
                group.Count(o => o.Protocol == 6), group.Count(o => o.Protocol == 17)));
        }
        return result.OrderByDescending(p => p.TotalConnections).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Pid).ToList();
    }

    private static bool IsSystemExecutable(string name, string? path, string windowsDirectory)
    {
        if (path == null)
        {
            return name.Equals("System", StringComparison.OrdinalIgnoreCase) || name.Equals("System.exe", StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrEmpty(windowsDirectory))
        {
            return false;
        }

        var fullPath = System.IO.Path.GetFullPath(path);
        return new[] { "System32", "SysWOW64" }.Any(directory =>
            fullPath.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.Combine(windowsDirectory, directory)) +
                System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    [SupportedOSPlatform("windows")]
    internal static (string Name, string? Path)? ReadIdentity(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var name = process.ProcessName + ".exe";
            // QUERY_LIMITED_INFORMATION also works for most elevated processes without inspecting memory.
            using var handle = OpenProcess(0x1000, false, pid);
            var path = new StringBuilder(32768);
            var length = path.Capacity;
            return !handle.IsInvalid && QueryFullProcessImageName(handle, 0, path, ref length)
                ? (System.IO.Path.GetFileName(path.ToString()), path.ToString()) : (name, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { return null; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(Microsoft.Win32.SafeHandles.SafeProcessHandle process, uint flags, StringBuilder path, ref int length);
}
