using Microsoft.Win32;

namespace ServiceLib.Manager;

/// <summary>
/// Changes the Windows system time zone while v2rayN runs, either to the zone of the
/// proxy exit IP (Auto) or to a zone the user picked (Manual).
/// The original zone is persisted in the config so it can be restored on exit,
/// when the feature is turned off, or on the next start after a crash.
/// </summary>
public class TimeZoneSyncManager
{
    private static readonly Lazy<TimeZoneSyncManager> _instance = new(() => new());
    public static TimeZoneSyncManager Instance => _instance.Value;

    private const string _tag = "TimeZoneSyncManager";
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static Config Config => ConfigTextManager.Instance.Config;

    private static string TzUtilPath => Path.Combine(Environment.SystemDirectory, "tzutil.exe");

    /// <summary>
    /// Windows time zone ids known to this system, ordered by UTC offset, for the manual picker.
    /// </summary>
    public static List<string> GetSystemZoneIds()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.Id)
            .Select(tz => tz.Id)
            .ToList();
    }

    public static bool IsKnownWindowsZone(string? windowsId)
    {
        if (windowsId.IsNullOrEmpty())
        {
            return false;
        }
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Called with the IANA zone (e.g. "Asia/Tokyo") reported for the exit IP of the active connection.
    /// Only acts in Auto mode.
    /// </summary>
    public async Task OnExitIpInfoAsync(string? ianaId)
    {
        if (!Utils.IsWindows() || Config.GuiItem.TimeZoneSyncMode != ETimeZoneSyncMode.Auto)
        {
            return;
        }
        if (ianaId.IsNullOrEmpty())
        {
            Logging.SaveLog($"{_tag}: IP info returned no time zone, check the IP API URL");
            return;
        }

        // Only accept zones the OS knows about; never pass raw API data to a process.
        if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId)
            || !IsKnownWindowsZone(windowsId))
        {
            Logging.SaveLog($"{_tag}: unknown time zone '{ianaId}'");
            return;
        }

        await SwitchToAsync(windowsId, ianaId);
    }

    /// <summary>
    /// Brings the system zone in line with the current setting: restores it when Off,
    /// sets the chosen zone when Manual. Auto waits for the next connection check.
    /// Used at startup and after settings are saved.
    /// </summary>
    public async Task ApplyConfiguredAsync()
    {
        switch (Config.GuiItem.TimeZoneSyncMode)
        {
            case ETimeZoneSyncMode.Off:
                await RestoreAsync();
                break;

            case ETimeZoneSyncMode.Manual:
                var manualId = Config.GuiItem.ManualTimeZoneId;
                if (Utils.IsWindows() && IsKnownWindowsZone(manualId))
                {
                    await SwitchToAsync(manualId!, null);
                }
                break;
        }
    }

    /// <summary>
    /// Restores the time zone that was active before the first change, if any.
    /// </summary>
    public async Task RestoreAsync()
    {
        if (!Utils.IsWindows())
        {
            return;
        }
        var original = Config.GuiItem.OriginalTimeZoneId;
        if (original.IsNullOrEmpty())
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            var current = await GetCurrentAsync();
            if (string.Equals(current, original, StringComparison.OrdinalIgnoreCase)
                || await SetAsync(original))
            {
                Config.GuiItem.OriginalTimeZoneId = null;
                await ConfigTextManager.Instance.SaveConfig();
                NoticeManager.Instance.SendMessageEx(string.Format(ResUI.TimeZoneSyncRestored, original));
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SwitchToAsync(string windowsId, string? ianaId)
    {
        await _lock.WaitAsync();
        try
        {
            var current = await GetCurrentAsync();
            if (current.IsNullOrEmpty()
                || string.Equals(current, windowsId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Config.GuiItem.OriginalTimeZoneId.IsNullOrEmpty())
            {
                Config.GuiItem.OriginalTimeZoneId = current;
                await ConfigTextManager.Instance.SaveConfig();
            }

            if (await SetAsync(windowsId))
            {
                NoticeManager.Instance.SendMessageEx(string.Format(ResUI.TimeZoneSyncApplied, windowsId, ianaId ?? Config.GuiItem.TimeZoneSyncMode.ToString()));
                if (IsAutoTimeZoneEnabled())
                {
                    NoticeManager.Instance.SendMessageEx(ResUI.TimeZoneSyncAutoUpdateWarning);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the current zone as reported by tzutil, including a "_dstoff" suffix
    /// when daylight saving is disabled, so it can be restored exactly.
    /// </summary>
    private static async Task<string?> GetCurrentAsync()
    {
        var output = await Utils.GetCliWrapOutput(TzUtilPath, ["/g"]);
        return output?.Trim();
    }

    private static async Task<bool> SetAsync(string windowsId)
    {
        // Two arguments so CliWrap quotes the id (zone ids contain spaces).
        var output = await Utils.GetCliWrapOutput(TzUtilPath, ["/s", windowsId]);
        if (output == null)
        {
            Logging.SaveLog($"{_tag}: tzutil failed to set '{windowsId}'");
            return false;
        }
        TimeZoneInfo.ClearCachedData();
        return true;
    }

    /// <summary>
    /// "Set time zone automatically" is backed by the tzautoupdate service; Start = 3 means enabled.
    /// When it is on, Windows may switch the zone back on its own.
    /// </summary>
    private static bool IsAutoTimeZoneEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\tzautoupdate");
            return key?.GetValue("Start") is int start && start == 3;
        }
        catch
        {
            return false;
        }
    }
}
