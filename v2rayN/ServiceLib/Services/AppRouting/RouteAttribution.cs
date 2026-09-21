namespace ServiceLib.Services.AppRouting;

internal enum RouteDecisionKind { Selected, Unselected, Unresolved, Ambiguous }
internal sealed record RouteDecision(RouteDecisionKind Kind, RouteProcessKey? Process = null, AppRouteRule? Rule = null)
{
    public static readonly RouteDecision Unresolved = new(RouteDecisionKind.Unresolved);
    public static readonly RouteDecision Unselected = new(RouteDecisionKind.Unselected);
}

internal sealed class RoutePolicy(IReadOnlyList<AppRouteRule> rules, IEnumerable<int> excluded)
{
    public AppRouteMatcher Matcher { get; } = new(rules);
    public HashSet<int> Excluded { get; } = excluded.Append(Environment.ProcessId).ToHashSet();
    private readonly Dictionary<string, string> _signatures = rules.ToDictionary(r => r.Id, r => JsonUtils.Serialize(r));
    public bool Retains(AppRouteRule rule) => _signatures.TryGetValue(rule.Id, out var signature) && signature == JsonUtils.Serialize(rule);
}

/// <summary>Immutable indexed ownership and precomputed process decisions. No native calls on lookup.</summary>
internal sealed class RouteAttributionSnapshot
{
    private readonly Dictionary<RouteFlow, RouteDecision> _tcp;
    private readonly Dictionary<(IPAddress, ushort), RouteDecision> _udp;
    public long ReadAt { get; }

    public RouteAttributionSnapshot(IEnumerable<RouteOwnerTable.Row> tcp, IEnumerable<RouteOwnerTable.Row> udp,
        Func<int, RouteDecision> decide, long readAt)
    {
        ReadAt = readAt;
        _tcp = tcp.GroupBy(r => new RouteFlow(6, r.Local, r.Port, r.Remote!, r.RemotePort))
            .ToDictionary(g => g.Key, g => Merge(g.Select(r => r.Pid).Distinct().Select(decide)));
        _udp = udp.GroupBy(r => (r.Local, r.Port))
            .ToDictionary(g => g.Key, g => Merge(g.Select(r => r.Pid).Distinct().Select(decide)));
    }

    public RouteDecision Find(RouteFlow flow)
    {
        if (flow.Protocol == 6) { return _tcp.GetValueOrDefault(flow, RouteDecision.Unresolved); }
        _udp.TryGetValue((flow.LocalAddress, flow.LocalPort), out var exact);
        var any = flow.LocalAddress.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any;
        _udp.TryGetValue((any, flow.LocalPort), out var wildcard);
        if (exact == null) { return wildcard ?? RouteDecision.Unresolved; }
        if (wildcard == null || exact == wildcard) { return exact; }
        return Merge([exact, wildcard]);
    }

    private static RouteDecision Merge(IEnumerable<RouteDecision> decisions)
    {
        var owners = decisions.Distinct().ToArray();
        if (owners.Length == 1) { return owners[0]; }
        if (owners.Any(d => d.Kind is RouteDecisionKind.Selected or RouteDecisionKind.Ambiguous)) { return new(RouteDecisionKind.Ambiguous); }
        return owners.Any(d => d.Kind == RouteDecisionKind.Unresolved) ? RouteDecision.Unresolved : RouteDecision.Unselected;
    }
}

/// <summary>Only background preparation/refresh calls this source. Process handles and ancestry
/// are retained across policy changes; each result is a complete immutable packet-path snapshot.</summary>
[SupportedOSPlatform("windows")]
internal sealed class RouteAttributionSource : IDisposable
{
    private readonly object _gate = new();
    private readonly RouteProcessSnapshot _processes = new();
    private readonly RouteProcessTree _tree = new(new([]), []);

    public RouteAttributionSnapshot Read(RoutePolicy policy)
    {
        lock (_gate)
        {
            var readAt = Environment.TickCount64;
            var processes = _processes.Read();
            _tree.SetRules(policy.Matcher, policy.Excluded);
            _tree.Update(processes);
            var decisions = processes.GroupBy(p => p.Key).Select(g => g.Last()).Where(p => p.Exited == null).ToDictionary(p => p.Key.Pid, p =>
            {
                if (policy.Excluded.Contains(p.Key.Pid) || AppRoutingManager.IsProtectedExecutable(p.Name)) { return RouteDecision.Unselected; }
                if (p.Path == null && policy.Matcher.NeedsPath(p.Name)) { return RouteDecision.Unresolved; }
                var rule = policy.Matcher.IncludesChildren ? _tree.Find(p.Key) : policy.Matcher.Find(p.Path, p.Name);
                return new RouteDecision(rule == null ? RouteDecisionKind.Unselected : RouteDecisionKind.Selected, p.Key, rule);
            });
            var tcp = new List<RouteOwnerTable.Row>();
            var udp = new List<RouteOwnerTable.Row>();
            foreach (var family in new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 })
            {
                if (family == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6) { continue; }
                tcp.AddRange(RouteOwnerTable.Read(6, family));
                udp.AddRange(RouteOwnerTable.Read(17, family));
            }
            return new(tcp, udp, pid => pid <= 4 || policy.Excluded.Contains(pid)
                ? RouteDecision.Unselected : decisions.GetValueOrDefault(pid, RouteDecision.Unresolved), readAt);
        }
    }

    public void Dispose() { lock (_gate) { _processes.Dispose(); } }
}
