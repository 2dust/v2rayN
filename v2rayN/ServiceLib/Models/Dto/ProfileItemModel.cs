namespace ServiceLib.Models.Dto;

public partial class ProfileItemModel : ReactiveObject
{
    public bool IsActive { get; set; }
    public string IndexId { get; set; }
    public EConfigType ConfigType { get; set; }
    public string Remarks { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string Network { get; set; }
    public string StreamSecurity { get; set; }
    public string Subid { get; set; }
    public string SubRemarks { get; set; }
    public int Sort { get; set; }

    [Reactive]
    public partial int Delay { get; set; }

    public decimal Speed { get; set; }

    [Reactive]
    public partial string DelayVal { get; set; }

    [Reactive]
    public partial string SpeedVal { get; set; }

    [Reactive]
    public partial string IpInfo { get; set; }

    [Reactive]
    public partial string TodayUp { get; set; }

    [Reactive]
    public partial string TodayDown { get; set; }

    [Reactive]
    public partial string TotalUp { get; set; }

    [Reactive]
    public partial string TotalDown { get; set; }

    public string GetSummary()
    {
        var summary = $"[{ConfigType}] {Remarks}";
        if (!ConfigType.IsComplexType())
        {
            summary += $"({Address}:{Port})";
        }

        return summary;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (IsActive)
        {
            parts.Add(ResUI.TipActiveServer);
        }
        if (!string.IsNullOrWhiteSpace(Remarks))
        {
            parts.Add(Remarks);
        }
        parts.Add(ConfigType.ToString());
        if (!ConfigType.IsComplexType() && !string.IsNullOrWhiteSpace(Address))
        {
            parts.Add(Port > 0 ? $"{Address}:{Port}" : Address);
        }
        if (!string.IsNullOrWhiteSpace(Network))
        {
            parts.Add(Network);
        }
        if (!string.IsNullOrWhiteSpace(StreamSecurity))
        {
            parts.Add(StreamSecurity);
        }
        if (!string.IsNullOrWhiteSpace(DelayVal))
        {
            parts.Add($"{ResUI.LvTestDelay} {DelayVal}");
        }
        if (!string.IsNullOrWhiteSpace(SpeedVal))
        {
            parts.Add($"{ResUI.LvTestSpeed} {SpeedVal}");
        }
        if (!string.IsNullOrWhiteSpace(IpInfo))
        {
            parts.Add(IpInfo);
        }
        if (!string.IsNullOrWhiteSpace(SubRemarks))
        {
            parts.Add(SubRemarks);
        }

        return string.Join(", ", parts);
    }
}
