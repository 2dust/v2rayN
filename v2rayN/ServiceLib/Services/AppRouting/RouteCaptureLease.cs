namespace ServiceLib.Services.AppRouting;

/// <summary>A named kernel object gives capture machine-wide ownership without a thread-affine mutex.</summary>
[SupportedOSPlatform("windows")]
internal sealed class RouteCaptureLease : IDisposable
{
    private readonly EventWaitHandle _handle;

    public RouteCaptureLease(string name = @"Global\v2rayN.ApplicationRouting")
    {
        _handle = new EventWaitHandle(false, EventResetMode.ManualReset, name, out var created);
        if (!created)
        {
            _handle.Dispose();
            throw new InvalidOperationException("Application routing is already owned by another v2rayN instance.");
        }
    }

    public void Dispose() => _handle.Dispose();
}
