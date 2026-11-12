namespace ServiceLib.Manager;

internal sealed class PendingCoreReload
{
    private int _pending;

    public void Request(bool forceRestart) => Interlocked.Or(ref _pending, forceRestart ? 2 : 1);

    public async Task RunPendingAsync(Func<bool, Task> reload)
    {
        var pending = Interlocked.Exchange(ref _pending, 0);
        if (pending != 0)
        {
            await reload((pending & 2) != 0);
        }
    }
}
