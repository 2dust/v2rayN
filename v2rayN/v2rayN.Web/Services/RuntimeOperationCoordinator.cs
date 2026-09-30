namespace v2rayN.Web.Services;

/// <summary>
/// Tracks stateful runtime work. Normal operations may overlap; maintenance operations
/// (core updates and restore) take an exclusive lease. Exclusive waiters prevent new
/// normal work from starving them, and shutdown cancels all linked operation tokens.
/// </summary>
public sealed class RuntimeOperationCoordinator
{
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(10);
    private readonly object _sync = new();
    private readonly CancellationTokenSource _shutdown = new();
    private TaskCompletionSource _changed = NewSignal();
    private int _activeOperations;
    private int _waitingExclusive;
    private bool _exclusiveActive;
    private bool _exclusiveAllowsObservations;
    private bool _stopping;

    public CancellationToken ShutdownToken => _shutdown.Token;
    public bool IsStopping
    {
        get
        {
            lock (_sync) return _stopping;
        }
    }

    public void RejectNewOperations()
    {
        lock (_sync)
        {
            _stopping = true;
            SignalChange();
        }
    }

    public ValueTask<Lease> EnterOperationAsync(CancellationToken cancellationToken = default) =>
        EnterAsync(exclusive: false, allowReadOnlyObservation: false, cancellationToken);

    /// <summary>
    /// Acquires a shared lease for an observational request such as status, operations, or logs.
    /// During selected maintenance windows these requests may also run concurrently with the
    /// exclusive operation; they must remain read-only and must not access resources being swapped.
    /// </summary>
    public ValueTask<Lease> EnterObservationAsync(CancellationToken cancellationToken = default) =>
        EnterAsync(exclusive: false, allowReadOnlyObservation: true, cancellationToken);

    public ValueTask<Lease> EnterExclusiveAsync(
        CancellationToken cancellationToken = default,
        bool allowReadOnlyObservations = false) =>
        EnterAsync(exclusive: true, allowReadOnlyObservation: false, cancellationToken, allowReadOnlyObservations);

    private async ValueTask<Lease> EnterAsync(
        bool exclusive,
        bool allowReadOnlyObservation,
        CancellationToken cancellationToken,
        bool allowReadOnlyObservations = false)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        var waiting = false;
        try
        {
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                Task waitTask;
                lock (_sync)
                {
                    if (_stopping)
                    {
                        throw new OperationCanceledException(_shutdown.Token);
                    }
                    if (linked.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(linked.Token);
                    }

                    if (exclusive && !waiting)
                    {
                        _waitingExclusive++;
                        waiting = true;
                    }

                    if (exclusive && !_exclusiveActive && _activeOperations == 0)
                    {
                        _waitingExclusive--;
                        waiting = false;
                        _exclusiveActive = true;
                        _exclusiveAllowsObservations = allowReadOnlyObservations;
                        return new Lease(this, exclusive: true, linked);
                    }

                    if (!exclusive && !_exclusiveActive && _waitingExclusive == 0)
                    {
                        _activeOperations++;
                        return new Lease(this, exclusive: false, linked);
                    }

                    if (allowReadOnlyObservation
                        && _exclusiveActive
                        && _exclusiveAllowsObservations
                        && _waitingExclusive == 0)
                    {
                        // Count observations as active so shutdown drains them and a subsequent
                        // exclusive operation cannot begin while one is still running.
                        _activeOperations++;
                        return new Lease(this, exclusive: false, linked);
                    }

                    waitTask = _changed.Task;
                }

                await waitTask.WaitAsync(linked.Token);
            }
        }
        catch
        {
            linked.Dispose();
            if (waiting)
            {
                lock (_sync)
                {
                    _waitingExclusive--;
                    SignalChange();
                }
            }
            throw;
        }
    }

    public async Task<bool> StopAndDrainAsync(CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        lock (_sync)
        {
            _stopping = true;
            SignalChange();
        }

        using var drainCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCancellation.CancelAfter(timeout ?? DefaultDrainTimeout);

        try
        {
            await _shutdown.CancelAsync().WaitAsync(drainCancellation.Token);
            while (true)
            {
                Task waitTask;
                lock (_sync)
                {
                    if (_activeOperations == 0 && !_exclusiveActive && _waitingExclusive == 0)
                    {
                        return true;
                    }
                    waitTask = _changed.Task;
                }

                await waitTask.WaitAsync(drainCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (drainCancellation.IsCancellationRequested)
        {
            return false;
        }
    }

    private void Release(bool exclusive)
    {
        lock (_sync)
        {
            if (exclusive)
            {
                _exclusiveActive = false;
                _exclusiveAllowsObservations = false;
            }
            else
            {
                _activeOperations--;
            }
            SignalChange();
        }
    }

    private void SignalChange()
    {
        var old = _changed;
        _changed = NewSignal();
        old.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public sealed class Lease : IAsyncDisposable, IDisposable
    {
        private RuntimeOperationCoordinator? _owner;
        private readonly bool _exclusive;
        private readonly CancellationTokenSource _linked;

        internal Lease(RuntimeOperationCoordinator owner, bool exclusive, CancellationTokenSource linked)
        {
            _owner = owner;
            _exclusive = exclusive;
            _linked = linked;
        }

        public CancellationToken Token => _linked.Token;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null)
            {
                return;
            }
            _linked.Dispose();
            owner.Release(_exclusive);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
