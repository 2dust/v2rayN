using v2rayN.Web.Contracts;
using ServiceLib.Common;

namespace v2rayN.Web.Services;

public sealed class LogBuffer
{
    private const int Capacity = 2000;
    private const int MaximumMessageCharacters = 16 * 1024;
    private const int MaximumRetainedCharacters = 1_000_000;
    private readonly object _gate = new();
    private readonly Queue<LogView> _items = new();
    private int _retainedCharacters;
    private long _generation;

    public LogView Add(string source, string message)
    {
        var normalized = message.TrimEnd();
        if (normalized.Length > MaximumMessageCharacters)
        {
            const string marker = "… [log entry truncated]";
            normalized = normalized[..(MaximumMessageCharacters - marker.Length)] + marker;
        }
        LogView entry;
        lock (_gate)
        {
            entry = new LogView(DateTimeOffset.UtcNow, source, normalized, _generation);
            _items.Enqueue(entry);
            _retainedCharacters += entry.Source.Length + entry.Message.Length;
            while (_items.Count > Capacity || _retainedCharacters > MaximumRetainedCharacters)
            {
                var removed = _items.Dequeue();
                _retainedCharacters -= removed.Source.Length + removed.Message.Length;
            }
        }

        return entry;
    }

    public IReadOnlyList<LogView> Recent(int limit, string? filter = null)
    {
        lock (_gate)
        {
            var query = _items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(item => Utils.IsRegexMatch($"{item.Source} {item.Message}", filter));
            }
            return query.TakeLast(Math.Clamp(limit, 1, Capacity)).ToArray();
        }
    }

    public LogPageView RecentPage(int page, int pageSize, string? filter = null)
    {
        lock (_gate)
        {
            var filtered = _items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                filtered = filtered.Where(item => Utils.IsRegexMatch($"{item.Source} {item.Message}", filter));
            }

            var items = filtered.ToArray();
            var total = items.Length;
            var safePageSize = Math.Clamp(pageSize, 1, 200);
            var totalPages = Math.Max(1, (total + safePageSize - 1) / safePageSize);
            var safePage = Math.Clamp(page, 1, totalPages);
            var end = total - ((safePage - 1) * safePageSize);
            var start = Math.Max(0, end - safePageSize);

            return new LogPageView(items.Skip(start).Take(end - start).ToArray(), safePage, safePageSize, total, totalPages);
        }
    }

    public long Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _retainedCharacters = 0;
            return ++_generation;
        }
    }
}
