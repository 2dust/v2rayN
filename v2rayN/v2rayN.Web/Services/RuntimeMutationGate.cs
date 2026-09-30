namespace v2rayN.Web.Services;

/// <summary>
/// Serializes Web-owned mutations of ServiceLib's shared configuration and SQLite state.
/// Subscription updates hold the gate across ServiceLib's download/import operation so their
/// profile, configuration, and timestamp changes remain ordered with other Web mutations.
/// </summary>
public sealed class RuntimeMutationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task RunAsync(Func<Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await mutation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await mutation();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
