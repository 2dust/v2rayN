namespace ServiceLib.Models;

public class SubscriptionUsageInfo
{
    public string SubId { get; set; } = string.Empty;

    // Bytes
    public long Upload { get; set; }
    public long Download { get; set; }
    public long Total { get; set; }

    // Unix epoch seconds; 0 if unknown
    public long ExpireEpoch { get; set; }

    public long UsedBytes => Math.Max(0, Upload + Download);

    public int UsagePercent
    {
        get
        {
            if (Total <= 0) return -1;
            var p = (int)Math.Round(UsedBytes * 100.0 / Total);
            return Math.Clamp(p, 0, 100);
        }
    }

    public DateTimeOffset? ExpireAt
    {
        get
        {
            if (ExpireEpoch <= 0) return null;
            try { return DateTimeOffset.FromUnixTimeSeconds(ExpireEpoch).ToLocalTime(); }
            catch { return null; }
        }
    }

    public int DaysLeft
    {
        get
        {
            var exp = ExpireAt;
            if (exp == null) return -1;
            var days = (int)Math.Ceiling((exp.Value - DateTimeOffset.Now).TotalDays);
            return Math.Max(days, 0);
        }
    }
}

