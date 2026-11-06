namespace v2rayN.WebAPI.Services;

/// <summary>
/// Serializes every Web-owned GeoFiles transaction, regardless of whether it was started
/// manually, as part of a batch update, or by a regional preset.
/// </summary>
internal sealed class GeoFilesUpdateGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task RunAsync(Func<Task> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await update();
        }
        finally
        {
            _gate.Release();
        }
    }
}
