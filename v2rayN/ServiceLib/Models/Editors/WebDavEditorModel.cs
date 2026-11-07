namespace ServiceLib.Models.Editors;

public partial class WebDavEditorModel : ReactiveObject
{
    private readonly WebDavItem _snapshot;

    private WebDavEditorModel(WebDavItem snapshot)
    {
        _snapshot = snapshot;
        Url = snapshot.Url;
        UserName = snapshot.UserName;
        Password = snapshot.Password;
        DirName = snapshot.DirName;
    }

    [Reactive]
    public partial string? Url { get; set; }

    [Reactive]
    public partial string? UserName { get; set; }

    [Reactive]
    public partial string? Password { get; set; }

    [Reactive]
    public partial string? DirName { get; set; }

    public static WebDavEditorModel FromEntity(WebDavItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var snapshot = JsonUtils.DeepCopy(entity)
            ?? throw new InvalidOperationException("Could not copy WebDavItem for editing.");
        return new WebDavEditorModel(snapshot);
    }

    public WebDavItem ToEntity()
    {
        var entity = JsonUtils.DeepCopy(_snapshot)
            ?? throw new InvalidOperationException("Could not copy WebDavItem for saving.");
        entity.Url = Url;
        entity.UserName = UserName;
        entity.Password = Password;
        entity.DirName = DirName;
        return entity;
    }
}
