namespace ServiceLib.Enums;

public enum ETimeZoneSyncMode
{
    /// <summary>Leave the system time zone alone.</summary>
    Off,

    /// <summary>Follow the time zone of the proxy exit IP.</summary>
    Auto,

    /// <summary>Use a time zone chosen by the user while v2rayN runs.</summary>
    Manual,
}
