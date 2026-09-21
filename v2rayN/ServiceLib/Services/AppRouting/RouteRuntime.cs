namespace ServiceLib.Services.AppRouting;

public enum AppRoutingState { Stopped, Starting, Running, Stopping, Faulted }

internal interface IRouteEngine : IAsyncDisposable
{
    Task<Exception?> Completion { get; }
    // Cancellation/failure is allowed before commit only. A successful return owns the new policy.
    Task ApplyAsync(IReadOnlyList<AppRouteRule> rules, IEnumerable<int> excludedProcesses, CancellationToken token);
    void Start();
}

internal interface IRouteProfile : IAsyncDisposable
{
    AppRouteRule Endpoint { get; }
    int ProcessId { get; }
    Task Completion { get; }
}

internal sealed record RouteProfilePlan(string Key, Func<CancellationToken, Task<IRouteProfile>> Start);
internal sealed record RouteRuntimePlan(IReadOnlyList<AppRouteRule> Rules, IReadOnlyDictionary<string, RouteProfilePlan> Profiles);

/// <summary>Stages dependencies before applying a policy to one persistent capture engine.
/// The manager serializes calls; a failed preparation leaves the current runtime intact.</summary>
internal sealed class RouteRuntime(Func<IRouteEngine> createEngine, Func<IDisposable> acquireLease)
{
    private IRouteEngine? _engine;
    private IDisposable? _lease;
    private Dictionary<string, IRouteProfile> _profiles = [];
    public bool IsRunning => _engine != null;
    public long Generation { get; private set; }

    public async Task<Exception?> WaitForFailureAsync(CancellationToken token)
    {
        var engine = _engine!.Completion;
        var finished = await Task.WhenAny(_profiles.Values.Select(p => p.Completion).Append(engine)).WaitAsync(token);
        return finished == engine ? await engine : new IOException("An application-routing Xray core exited unexpectedly.");
    }

    public async Task ApplyAsync(RouteRuntimePlan plan, CancellationToken token)
    {
        var created = new Dictionary<string, IRouteProfile>();
        var next = new Dictionary<string, IRouteProfile>();
        IRouteEngine? candidate = null;
        var lease = _lease ?? acquireLease();
        try
        {
            foreach (var profile in plan.Profiles.Values.DistinctBy(p => p.Key))
            {
                token.ThrowIfCancellationRequested();
                if (!_profiles.TryGetValue(profile.Key, out var instance) || instance.Completion.IsCompleted)
                {
                    instance = await profile.Start(token);
                    created.Add(profile.Key, instance);
                }
                next.Add(profile.Key, instance);
            }
            var rules = JsonUtils.DeepCopy(plan.Rules.ToList());
            foreach (var rule in rules.Where(r => r.Kind == AppRouteKind.Profile))
            {
                var endpoint = next[plan.Profiles[rule.Id].Key].Endpoint;
                rule.Kind = AppRouteKind.Socks5;
                rule.SocksHost = endpoint.SocksHost;
                rule.SocksPort = endpoint.SocksPort;
                rule.SocksUsername = endpoint.SocksUsername;
                rule.SocksPassword = endpoint.SocksPassword;
            }
            candidate = _engine == null ? createEngine() : null;
            token.ThrowIfCancellationRequested();
            await (candidate ?? _engine!).ApplyAsync(rules, next.Values.Select(p => p.ProcessId), token);
            // Cancellation after commit belongs to StopAsync, which drains the new policy's resources.
            candidate?.Start();
            // No fallible preparation remains. Publish the new ownership before retiring old cores.
            _engine ??= candidate;
            _lease = lease;
            var retired = _profiles.Where(p => !next.TryGetValue(p.Key, out var current) || current != p.Value).Select(p => p.Value).ToArray();
            _profiles = next;
            Generation++;
            created.Clear();
            candidate = null;
            foreach (var profile in retired) { await profile.DisposeAsync(); }
        }
        catch
        {
            if (candidate != null) { await candidate.DisposeAsync(); }
            foreach (var profile in created.Values) { await profile.DisposeAsync(); }
            if (_lease == null) { lease.Dispose(); }
            throw;
        }
    }

    public async Task StopAsync()
    {
        Generation++;
        var engine = _engine;
        _engine = null;
        try
        {
            if (engine != null) { await engine.DisposeAsync(); }
        }
        finally
        {
            foreach (var profile in _profiles.Values) { await profile.DisposeAsync(); }
            _profiles.Clear();
            _lease?.Dispose();
            _lease = null;
        }
    }
}
