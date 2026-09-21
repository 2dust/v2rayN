using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class RuntimeTests
{
    private sealed class Lease : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }
    private sealed class Engine(List<string> events) : IRouteEngine
    {
        public readonly TaskCompletionSource<Exception?> End = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<Exception?> Completion => End.Task;
        public IReadOnlyList<AppRouteRule> Rules = [];
        public bool Reject;
        public Action? OnCommit;
        public Task ApplyAsync(IReadOnlyList<AppRouteRule> rules, IEnumerable<int> excludedProcesses, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Reject) { throw new IOException("apply failed"); }
            Rules = rules;
            events.Add("apply");
            OnCommit?.Invoke();
            return Task.CompletedTask;
        }
        public void Start() => events.Add("start");
        public ValueTask DisposeAsync() { events.Add("stop"); End.TrySetResult(null); return ValueTask.CompletedTask; }
    }
    private sealed class Profile(string name, int port, List<string> events) : IRouteProfile
    {
        public readonly TaskCompletionSource End = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => End.Task;
        public AppRouteRule Endpoint { get; } = new() { SocksPort = port };
        public int ProcessId => port;
        public bool Disposed;
        public ValueTask DisposeAsync() { Disposed = true; events.Add("dispose " + name); End.TrySetResult(); return ValueTask.CompletedTask; }
    }
    private static AppRouteRule Rule() => new() { Id = "app", ExecutablePath = "app.exe", MatchByName = true, Kind = AppRouteKind.Profile };
    private static RouteRuntimePlan Plan(AppRouteRule rule, string key, Func<CancellationToken, Task<IRouteProfile>> start) =>
        new([rule], new Dictionary<string, RouteProfilePlan> { [rule.Id] = new(key, start) });

    [Test]
    public async Task CancellationAtCommitKeepsNewResourcesUntilExplicitStop()
    {
        var events = new List<string>();
        using var stop = new CancellationTokenSource();
        var engine = new Engine(events) { OnCommit = stop.Cancel };
        var profile = new Profile("core", 10001, events);
        var lease = new Lease();
        var runtime = new RouteRuntime(() => engine, () => lease);
        try
        {
            await runtime.ApplyAsync(Plan(Rule(), "one", _ => Task.FromResult<IRouteProfile>(profile)), stop.Token);
            await stop.IsCancellationRequested.Should().BeTrue();
            await runtime.IsRunning.Should().BeTrue();
            await profile.Disposed.Should().BeFalse();
            await lease.Disposed.Should().BeFalse();
        }
        finally { await runtime.StopAsync(); }
        await profile.Disposed.Should().BeTrue();
        await lease.Disposed.Should().BeTrue();
    }

    [Test]
    public async Task LaterPreparationFailureDisposesEarlierCandidateButKeepsSharedCore()
    {
        var events = new List<string>();
        var engine = new Engine(events);
        var runtime = new RouteRuntime(() => engine, () => new Lease());
        var old = new Profile("old", 10001, events);
        var candidate = new Profile("candidate", 10002, events);
        var rule = Rule();
        var another = Rule();
        another.Id = "another";
        try
        {
            var original = Plan(rule, "old", _ => Task.FromResult<IRouteProfile>(old));
            await runtime.ApplyAsync(original, default);
            var failed = false;
            try
            {
                await runtime.ApplyAsync(new([rule, another], new Dictionary<string, RouteProfilePlan>
                {
                    [rule.Id] = original.Profiles[rule.Id],
                    [another.Id] = new("candidate", _ => Task.FromResult<IRouteProfile>(candidate)),
                    ["fails"] = new("fails", _ => throw new IOException("second candidate failed"))
                }), default);
            }
            catch (IOException) { failed = true; }
            await failed.Should().BeTrue();
            await old.Disposed.Should().BeFalse();
            await candidate.Disposed.Should().BeTrue();
            await engine.Rules.Single().SocksPort.Should().BeEqualTo(10001);
        }
        finally { await runtime.StopAsync(); }
    }

    [Test]
    public async Task ReplacementPreparesWhileOldPolicyRunsAndReusesUnchangedCore()
    {
        var events = new List<string>();
        var engine = new Engine(events);
        var lease = new Lease();
        var runtime = new RouteRuntime(() => engine, () => lease);
        var old = new Profile("old", 10001, events);
        var next = new Profile("next", 10002, events);
        var rule = Rule();
        var ready = new TaskCompletionSource<IRouteProfile>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await runtime.ApplyAsync(Plan(rule, "one", _ => Task.FromResult<IRouteProfile>(old)), default);
            await runtime.ApplyAsync(Plan(rule, "one", _ => throw new Exception("must reuse")), default);
            var apply = runtime.ApplyAsync(Plan(rule, "two", _ => ready.Task), default);
            await runtime.IsRunning.Should().BeTrue();
            await engine.Rules.Single().SocksPort.Should().BeEqualTo(10001);
            await old.Disposed.Should().BeFalse();
            ready.SetResult(next);
            await apply;
            await engine.Rules.Single().SocksPort.Should().BeEqualTo(10002);
            await events.SequenceEqual(new[] { "apply", "start", "apply", "apply", "dispose old" }).Should().BeTrue();
            await rule.Kind.Should().BeEqualTo(AppRouteKind.Profile);
            await lease.Disposed.Should().BeFalse();
        }
        finally { ready.TrySetResult(next); await runtime.StopAsync(); }
        await lease.Disposed.Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedReplacementLeavesOldResourcesAndPolicyIntact(bool applyFailure)
    {
        var events = new List<string>();
        var engine = new Engine(events);
        var lease = new Lease();
        var runtime = new RouteRuntime(() => engine, () => lease);
        var old = new Profile("old", 10001, events);
        var candidate = new Profile("candidate", 10002, events);
        var rule = Rule();
        try
        {
            await runtime.ApplyAsync(Plan(rule, "old", _ => Task.FromResult<IRouteProfile>(old)), default);
            engine.Reject = applyFailure;
            var failed = false;
            try
            {
                await runtime.ApplyAsync(Plan(rule, "new", _ => applyFailure ? Task.FromResult<IRouteProfile>(candidate) : throw new IOException("startup failed")), default);
            }
            catch (IOException) { failed = true; }
            await failed.Should().BeTrue();
            await runtime.IsRunning.Should().BeTrue();
            await old.Disposed.Should().BeFalse();
            await lease.Disposed.Should().BeFalse();
            await candidate.Disposed.Should().BeEqualTo(applyFailure);
            await engine.Rules.Single().SocksPort.Should().BeEqualTo(10001);
        }
        finally { await runtime.StopAsync(); }
    }

    [Test]
    public async Task CancelledInitialPreparationReleasesLeaseWithoutStartingCapture()
    {
        var events = new List<string>();
        var lease = new Lease();
        var runtime = new RouteRuntime(() => new Engine(events), () => lease);
        using var stop = new CancellationTokenSource();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = runtime.ApplyAsync(Plan(Rule(), "slow", async token =>
        {
            waiting.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new Exception("unreachable");
        }), stop.Token);
        await waiting.Task;
        stop.Cancel();
        var cancelled = false;
        try { await start; } catch (OperationCanceledException) { cancelled = true; }
        await cancelled.Should().BeTrue();
        await events.Count.Should().BeEqualTo(0);
        await lease.Disposed.Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BothCoreExitAndEngineFailureAreObservable(bool coreExit)
    {
        var events = new List<string>();
        var engine = new Engine(events);
        var profile = new Profile("core", 10001, events);
        var runtime = new RouteRuntime(() => engine, () => new Lease());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await runtime.ApplyAsync(Plan(Rule(), "one", _ => Task.FromResult<IRouteProfile>(profile)), timeout.Token);
            var failure = runtime.WaitForFailureAsync(timeout.Token);
            if (coreExit) { profile.End.SetResult(); } else { engine.End.SetResult(new IOException("capture failed")); }
            await ((await failure) is IOException).Should().BeTrue();
        }
        finally { await runtime.StopAsync(); }
    }

    [Test]
    public async Task CaptureLeaseIsExclusiveAndCanBeReleasedOnAnotherThread()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var name = @"Global\v2rayN.ApplicationRouting.Test." + Guid.NewGuid().ToString("N");
        var first = new RouteCaptureLease(name);
        try
        {
            var rejected = false;
            try { using var second = new RouteCaptureLease(name); }
            catch (InvalidOperationException) { rejected = true; }
            await rejected.Should().BeTrue();
        }
        finally { await Task.Run(first.Dispose); }
        using var next = new RouteCaptureLease(name);
    }
}
