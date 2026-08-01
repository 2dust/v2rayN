namespace ServiceLib.Enums;

/// <summary>
/// Groups the speed test actions by the toolbar button that starts and stops them.
/// <para>
/// Each button owns its group and only ever stops its own group. The groups may not run at the
/// same time though: the speed group runs a real ping before each download, so it writes the
/// delay column as well, and overlapping runs would have both of them writing delays for the
/// same profiles while competing for core processes and local ports. Starting one group
/// therefore stops the other first.
/// </para>
/// </summary>
public enum ESpeedTestGroup
{
    /// <summary>Measures delay only (tcping, real ping, UDP).</summary>
    Delay,

    /// <summary>Measures delay and download speed.</summary>
    Speed
}
