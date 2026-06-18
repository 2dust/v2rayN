using System.Globalization;

namespace ServiceLib.Models.Entities;

[Serializable]
public class SubItem : System.ComponentModel.INotifyPropertyChanged
{
    [field: NonSerialized]
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    [PrimaryKey]
    public string Id { get; set; }

    public string Remarks { get; set; }

    public string Url { get; set; }

    public string MoreUrl { get; set; }

    public bool Enabled { get; set; } = true;

    public string UserAgent { get; set; } = string.Empty;

    public int Sort { get; set; }

    public string? Filter { get; set; }

    public int AutoUpdateInterval { get; set; }

    public long UpdateTime { get; set; }

    public string? ConvertTarget { get; set; }

    public string? PrevProfile { get; set; }

    public string? NextProfile { get; set; }

    public int? PreSocksPort { get; set; }

    public string? Memo { get; set; }

    [Ignore]
    public string RemarksWithUpdateTime
    {
        get
        {
            if (Id.IsNullOrEmpty())
            {
                return Remarks;
            }

            return $"{Remarks} · {UpdateTimeAgo}";
        }
    }

    [Ignore]
    public string UpdateTimeAgo => FormatUpdateTimeAgo(UpdateTime);

    public void RefreshUpdateTimeAgo()
    {
        OnPropertyChanged(nameof(UpdateTimeAgo));
        OnPropertyChanged(nameof(RemarksWithUpdateTime));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public static string FormatUpdateTimeAgo(long updateTime, DateTimeOffset? now = null)
    {
        var isChinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (updateTime <= 0)
        {
            return isChinese ? "未更新" : "Never";
        }

        var currentTime = now ?? DateTimeOffset.Now;
        var updatedTime = DateTimeOffset.FromUnixTimeSeconds(updateTime).ToLocalTime();
        var elapsed = currentTime - updatedTime;
        if (elapsed.TotalSeconds < 0)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalMinutes < 1)
        {
            return isChinese ? "刚刚" : "Just now";
        }
        if (elapsed.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)elapsed.TotalMinutes);
            return isChinese ? $"{minutes} 分钟前" : $"{minutes} min ago";
        }
        if (elapsed.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)elapsed.TotalHours);
            return isChinese ? $"{hours} 小时前" : $"{hours} h ago";
        }
        if (elapsed.TotalDays < 7)
        {
            var days = Math.Max(1, (int)elapsed.TotalDays);
            return isChinese ? $"{days} 天前" : $"{days} d ago";
        }

        return updatedTime.ToString("yyyy/MM/dd HH:mm");
    }
}
