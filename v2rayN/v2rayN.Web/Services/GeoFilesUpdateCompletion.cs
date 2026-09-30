using System.Collections.Concurrent;

namespace v2rayN.Web.Services;

internal sealed class GeoFilesUpdateCompletion
{
    private readonly ConcurrentQueue<string> _failures = new();
    private int _completionReported;

    public void Report(bool success, string message, bool isProgressMessage)
    {
        if (success)
        {
            Interlocked.Exchange(ref _completionReported, 1);
        }
        else if (!isProgressMessage)
        {
            _failures.Enqueue(message);
        }
    }

    public void EnsureSuccessful()
    {
        if (!_failures.IsEmpty)
        {
            throw new IOException("GeoFiles update reported an error: " + string.Join("; ", _failures));
        }
        if (Volatile.Read(ref _completionReported) == 0)
        {
            throw new IOException("GeoFiles update did not report successful completion.");
        }
    }
}
