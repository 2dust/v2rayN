namespace ServiceLib.Manager;

internal sealed class PendingCoreReload
{
    private readonly object _sync = new();
    private bool _running;
    private int _pending;

    public bool TryStart(bool forceRestart)
    {
        lock (_sync)
        {
            if (!_running)
            {
                _running = true;
                return true;
            }
            _pending |= forceRestart ? 2 : 1;
            return false;
        }
    }

    public bool? Complete()
    {
        lock (_sync)
        {
            if (_pending == 0)
            {
                _running = false;
                return null;
            }
            var force = (_pending & 2) != 0;
            _pending = 0;
            // Keep ownership for the pending reload; new callers still join this owner.
            return force;
        }
    }
}
