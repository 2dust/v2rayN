using Microsoft.Win32;

namespace ServiceLib.Common;

[SupportedOSPlatform("windows")]
internal static class WindowsTunCompatibility
{
    private static readonly string _tag = "WindowsTunCompatibility";
    private const string AffectedBuildMessage = "Windows 26200.8524/26100.8524 TUN compatibility mode";

    public static bool IsAffectedBuild()
    {
        if (!Utils.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var currentBuild = key?.GetValue("CurrentBuildNumber")?.ToString();
            var ubrValue = key?.GetValue("UBR");

            if (!int.TryParse(currentBuild, out var build))
            {
                build = Environment.OSVersion.Version.Build;
            }

            var ubr = ubrValue switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };

            return (build is 26100 or 26200) && ubr >= 8524;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return Environment.OSVersion.Version.Build is 26100 or 26200;
        }
    }

    public static void ApplyAffectedBuildDefaults(TunModeItem item, out string? message)
    {
        message = null;

        if (!IsAffectedBuild())
        {
            return;
        }

        var changed = false;
        if (item.Mtu <= 0 || item.Mtu > 1500)
        {
            item.Mtu = 1500;
            changed = true;
        }

        if (item.StrictRoute)
        {
            item.StrictRoute = false;
            changed = true;
        }

        if (item.Stack.IsNullOrEmpty() || item.Stack.Equals("gvisor", StringComparison.OrdinalIgnoreCase))
        {
            item.Stack = "system";
            changed = true;
        }

        if (changed)
        {
            message = $"{AffectedBuildMessage}: stack=system, mtu=1500, strict_route=false";
            Logging.SaveLog($"{_tag}: {message}");
        }
    }
}
