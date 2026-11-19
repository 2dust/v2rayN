namespace ServiceLib.Enums;

/// <summary>
/// Strategy used by AutoSwitchManager when deciding whether to switch the
/// active server after a periodic real-ping test.
/// </summary>
public enum EAutoSwitchStrategy
{
    /// <summary>Auto switch is disabled.</summary>
    Off = 0,

    /// <summary>Switch only when the current server is unreachable or severely degraded.</summary>
    FailoverOnly = 1,

    /// <summary>Switch to the fastest server within the current server's region.</summary>
    SameRegion = 2,

    /// <summary>Switch to the fastest server overall (v1 behavior).</summary>
    Global = 3,
}
