namespace ServiceLib.Models.Editors;

public partial class SubscriptionEditorModel : ReactiveObject
{
    private readonly SubItem _snapshot;

    private SubscriptionEditorModel(SubItem snapshot)
    {
        _snapshot = snapshot;
        Remarks = snapshot.Remarks;
        Url = snapshot.Url;
        MoreUrl = snapshot.MoreUrl;
        Enabled = snapshot.Enabled;
        AutoUpdateInterval = snapshot.AutoUpdateInterval;
        UserAgent = snapshot.UserAgent;
        RequestHeaders = snapshot.RequestHeaders;
        Sort = snapshot.Sort;
        Filter = snapshot.Filter;
        ConvertTarget = snapshot.ConvertTarget;
        PreSocksPort = snapshot.PreSocksPort;
        Memo = snapshot.Memo;
        PreResolver = snapshot.PreResolver;
    }

    [Reactive]
    public partial string Remarks { get; set; }

    [Reactive]
    public partial string Url { get; set; }

    [Reactive]
    public partial string MoreUrl { get; set; }

    [Reactive]
    public partial bool Enabled { get; set; }

    [Reactive]
    public partial int AutoUpdateInterval { get; set; }

    [Reactive]
    public partial string UserAgent { get; set; }

    [Reactive]
    public partial string? RequestHeaders { get; set; }

    [Reactive]
    public partial int Sort { get; set; }

    [Reactive]
    public partial string? Filter { get; set; }

    [Reactive]
    public partial string? ConvertTarget { get; set; }

    [Reactive]
    public partial int? PreSocksPort { get; set; }

    [Reactive]
    public partial string? Memo { get; set; }

    [Reactive]
    public partial string? PreResolver { get; set; }

    public static SubscriptionEditorModel FromEntity(SubItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var snapshot = JsonUtils.DeepCopy(entity)
            ?? throw new InvalidOperationException("Could not copy SubItem for editing.");
        return new SubscriptionEditorModel(snapshot);
    }

    public SubItem ToEntity()
    {
        var entity = JsonUtils.DeepCopy(_snapshot)
            ?? throw new InvalidOperationException("Could not copy SubItem for saving.");
        entity.Remarks = Remarks;
        entity.Url = Url;
        entity.MoreUrl = MoreUrl;
        entity.Enabled = Enabled;
        entity.AutoUpdateInterval = AutoUpdateInterval;
        entity.UserAgent = UserAgent;
        entity.RequestHeaders = RequestHeaders;
        entity.Sort = Sort;
        entity.Filter = Filter;
        entity.ConvertTarget = ConvertTarget;
        entity.PreSocksPort = PreSocksPort;
        entity.Memo = Memo;
        entity.PreResolver = PreResolver;
        return entity;
    }
}
