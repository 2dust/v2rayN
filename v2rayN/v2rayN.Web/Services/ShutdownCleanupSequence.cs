using System.Diagnostics;

namespace v2rayN.Web.Services;

internal static class RuntimeShutdownBudgets
{
    // Runtime cleanup owns 20 seconds; the host retains 5 seconds, systemd is
    // configured to 30 seconds, and the native --stop launcher waits 5 seconds more.
    public static readonly TimeSpan RuntimeCleanup = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan HostShutdown = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan LauncherWait = TimeSpan.FromSeconds(35);
}

internal static class ShutdownDiagnostics
{
    private static string? _currentStage;

    public static string? CurrentStage => Volatile.Read(ref _currentStage);

    public static void SetStage(string? stage) => Volatile.Write(ref _currentStage, stage);
}

internal sealed class ShutdownDeadline : IDisposable
{
    private readonly TimeSpan _budget;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly CancellationTokenSource _deadlineCancellation;
    private readonly CancellationTokenSource _linkedCancellation;

    public ShutdownDeadline(TimeSpan budget, CancellationToken cancellationToken)
    {
        if (budget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        _budget = budget;
        _deadlineCancellation = new CancellationTokenSource(budget);
        _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _deadlineCancellation.Token);
    }

    public CancellationToken Token => _linkedCancellation.Token;

    public TimeSpan Remaining
    {
        get
        {
            var remaining = _budget - _elapsed.Elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public void Dispose()
    {
        _linkedCancellation.Dispose();
        _deadlineCancellation.Dispose();
    }
}

internal sealed record ShutdownCleanupStep(
    string Name,
    Func<ShutdownDeadline, Task<bool>> ExecuteAsync);

internal static class ShutdownCleanupSequence
{
    public static async Task<bool> RunAsync(
        IEnumerable<ShutdownCleanupStep> steps,
        TimeSpan budget,
        CancellationToken cancellationToken,
        Action<string> log,
        Action<string?>? stageChanged = null)
    {
        using var deadline = new ShutdownDeadline(budget, cancellationToken);
        foreach (var step in steps)
        {
            var remaining = deadline.Remaining;
            if (remaining <= TimeSpan.Zero)
            {
                log($"Shutdown deadline expired before '{step.Name}'; all later cleanup was skipped.");
                return false;
            }

            stageChanged?.Invoke(step.Name);
            try
            {
                // Run the synchronous prefix on the thread pool too, so a blocking cleanup
                // implementation cannot prevent the overall deadline from being enforced.
                var execution = Task.Run(async () => await step.ExecuteAsync(deadline));
                var completed = await execution.WaitAsync(remaining, deadline.Token);
                if (!completed)
                {
                    log($"Shutdown stopped at '{step.Name}'; all later cleanup was skipped.");
                    return false;
                }
                stageChanged?.Invoke(null);
            }
            catch (TimeoutException)
            {
                log($"Shutdown deadline expired during '{step.Name}'; all later cleanup was skipped.");
                return false;
            }
            catch (OperationCanceledException) when (deadline.Token.IsCancellationRequested)
            {
                log($"Shutdown was canceled during '{step.Name}'; all later cleanup was skipped.");
                return false;
            }
            catch (Exception exception)
            {
                log($"Shutdown cleanup failed at '{step.Name}': {exception.Message}; all later cleanup was skipped.");
                return false;
            }
        }

        stageChanged?.Invoke(null);
        return true;
    }
}
