namespace ServiceLib.Services.AppRouting;

internal sealed class RouteNatEntry(RouteFlow flow, AppRouteRule rule, ushort translatedPort, uint initialSequence)
{
    public RouteFlow Flow { get; } = flow;
    public AppRouteRule Rule { get; } = rule;
    public ushort TranslatedPort { get; } = translatedPort;
    public uint InitialSequence { get; } = initialSequence;
    private long _lastActivity = Environment.TickCount64;
    private volatile bool _accepted;
    private volatile bool _closed;
    private readonly object _relayGate = new();
    private CancellationTokenSource? _relayStop;
    public CancellationTokenSource BeginRelay(CancellationToken token)
    {
        lock (_relayGate)
        {
            _relayStop = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (Closed) { _relayStop.Cancel(); }
            return _relayStop;
        }
    }

    public void Retire()
    {
        lock (_relayGate)
        {
            Closed = true;
            LastActivity = Environment.TickCount64;
            try { _relayStop?.Cancel(); } catch (ObjectDisposedException) { }
        }
    }
    public long LastActivity
    {
        get => Interlocked.Read(ref _lastActivity);
        set => Interlocked.Exchange(ref _lastActivity, value);
    }
    public bool Accepted
    {
        get => _accepted;
        set => _accepted = value;
    }
    public bool Closed
    {
        get => _closed;
        set => _closed = value;
    }
    public DivertAddress OriginalAddress
    {
        get; set;
    }
}

/// <summary>Each original five-tuple gets an independent reflected connection.</summary>
internal sealed class RouteNatTable
{
    private readonly object _gate = new();
    private readonly Dictionary<RouteFlow, RouteNatEntry> _forward = [];
    private readonly Dictionary<ushort, RouteNatEntry> _reverse = [];
    private int _next = 1024;

    public RouteNatEntry GetOrAdd(RouteFlow flow, AppRouteRule rule, uint initialSequence)
    {
        lock (_gate)
        {
            if (_forward.TryGetValue(flow, out var entry))
            {
                return entry;
            }

            for (var i = 0; i < 64512; i++)
            {
                var port = (ushort)_next;
                _next = _next == 65535 ? 1024 : _next + 1;
                if (_reverse.ContainsKey(port))
                {
                    continue;
                }

                entry = new(flow, rule, port, initialSequence);
                _forward.Add(flow, entry);
                _reverse.Add(port, entry);
                return entry;
            }
            throw new IOException("Application routing connection limit reached.");
        }
    }

    public RouteNatEntry? Find(RouteFlow flow, uint? synSequence = null)
    {
        lock (_gate)
        {
            var entry = _forward.GetValueOrDefault(flow);
            // A fresh SYN may arrive before the previous relay finishes closing.
            // Retransmissions retain their initial sequence and keep the same mapping.
            if (entry != null && synSequence is uint sequence && (entry.Closed || entry.InitialSequence != sequence))
            {
                Retire(entry);
                return null;
            }
            return entry;
        }
    }

    private void Retire(RouteNatEntry entry)
    {
        // Keep the reverse entry briefly so late FIN/RST responses cannot escape onto the network.
        lock (_gate)
        {
            entry.Retire();
            if (ReferenceEquals(_forward.GetValueOrDefault(entry.Flow), entry))
            {
                _forward.Remove(entry.Flow);
            }
        }
    }

    public RouteNatEntry? Reverse(IPAddress local, IPAddress remote, ushort translatedPort)
    {
        lock (_gate)
        {
            var entry = _reverse.GetValueOrDefault(translatedPort);
            return entry != null && entry.Flow.LocalAddress.GetAddressBytes().AsSpan().SequenceEqual(local.GetAddressBytes()) &&
                entry.Flow.RemoteAddress.GetAddressBytes().AsSpan().SequenceEqual(remote.GetAddressBytes()) ? entry : null;
        }
    }

    public void Expire(long now)
    {
        lock (_gate)
        {
            foreach (var entry in _reverse.Values.Where(e => (e.Closed || !e.Accepted) &&
                         now - e.LastActivity > 120_000).ToArray())
            {
                Retire(entry);
                _reverse.Remove(entry.TranslatedPort);
            }
        }
    }

    public void Retain(RoutePolicy policy)
    {
        lock (_gate)
        {
            foreach (var entry in _forward.Values.Where(e => !policy.Retains(e.Rule)).ToArray()) { Retire(entry); }
        }
    }
}
