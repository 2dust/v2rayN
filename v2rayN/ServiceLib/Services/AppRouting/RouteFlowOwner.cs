namespace ServiceLib.Services.AppRouting;

/// <summary>Invalidates a UDP session when its local endpoint changes process owner.</summary>
internal sealed class RouteFlowOwner(RouteProcessKey owner, Func<RouteProcessKey?> readOwner)
{
    private readonly object _gate = new();
    private bool _current = true;

    public bool IsCurrent()
    {
        lock (_gate)
        {
            if (!_current)
            {
                return false;
            }
            // The reader uses an immutable index; do not add another cache delay.
            // Once ownership is lost this session stays invalid.
            _current = false;
            _current = readOwner() == owner;
            return _current;
        }
    }
}
