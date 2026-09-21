namespace ServiceLib.Services.AppRouting;

internal readonly record struct RouteProcessKey(int Pid, long Started);
internal sealed record RouteProcessInfo(RouteProcessKey Key, int ParentPid, string Name, string? Path, long? Exited = null);

/// <summary>Process identities include creation time; an inherited route never follows a recycled PID.</summary>
internal sealed class RouteProcessTree(AppRouteMatcher rules, IEnumerable<int> excludedProcesses)
{
    private readonly HashSet<int> _excluded = excludedProcesses.ToHashSet();
    private readonly Dictionary<RouteProcessKey, Node> _nodes = [];

    private sealed class Node(RouteProcessInfo info)
    {
        public RouteProcessInfo Info = info;
        public Node? Parent;
    }

    public bool Contains(RouteProcessKey key) => _nodes.ContainsKey(key);

    public void SetRules(AppRouteMatcher updated, IEnumerable<int> excluded)
    {
        rules = updated;
        _excluded.Clear();
        _excluded.UnionWith(excluded);
    }

    public void Update(IEnumerable<RouteProcessInfo> snapshot)
    {
        foreach (var info in snapshot)
        {
            if (_nodes.TryGetValue(info.Key, out var node))
            {
                node.Info = info;
            }
            else
            {
                _nodes.Add(info.Key, new(info));
            }
        }
        var byPid = _nodes.Values.GroupBy(n => n.Info.Key.Pid).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var node in _nodes.Values)
        {
            if (node.Parent?.Info.Exited < node.Info.Key.Started)
            {
                node.Parent = null;
            }

            if (node.Parent != null)
            {
                continue;
            }

            if (!byPid.TryGetValue(node.Info.ParentPid, out var parents))
            {
                continue;
            }

            node.Parent = parents.Where(p => p != node && p.Info.Key.Started <= node.Info.Key.Started &&
                    (p.Info.Exited == null || p.Info.Exited >= node.Info.Key.Started))
                .MaxBy(p => p.Info.Key.Started);
        }

        // Keep live ancestry plus a bounded history for children discovered just after a parent exits.
        var retained = new HashSet<RouteProcessKey>();
        foreach (var node in _nodes.Values.Where(n => n.Info.Exited == null)
            .Concat(_nodes.Values.Where(n => n.Info.Exited != null).OrderByDescending(n => n.Info.Exited).Take(2048)))
        {
            for (var ancestor = node; ancestor != null && retained.Add(ancestor.Info.Key); ancestor = ancestor.Parent)
            {
            }
        }

        foreach (var key in _nodes.Keys.Where(key => !retained.Contains(key)).ToList())
        {
            _nodes.Remove(key);
        }
    }

    public AppRouteRule? Find(RouteProcessKey key)
    {
        if (!_nodes.TryGetValue(key, out var node))
        {
            return null;
        }

        var seen = new HashSet<RouteProcessKey>();
        AppRouteRule? match = null;
        for (var ancestor = node; ancestor != null; ancestor = ancestor.Parent)
        {
            if (!seen.Add(ancestor.Info.Key))
            {
                return null;
            }

            if (_excluded.Contains(ancestor.Info.Key.Pid) || AppRoutingManager.IsProtectedExecutable(ancestor.Info.Name))
            {
                return null;
            }

            var rule = rules.Find(ancestor.Info.Path, ancestor.Info.Name);
            if (match == null && rule != null && (ancestor == node || rule.IncludeChildProcesses))
            {
                match = rule;
            }
        }
        return match;
    }
}
