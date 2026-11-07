namespace ServiceLib.Models.Editors;

public partial class ProfileEditorModel : ReactiveObject
{
    private readonly ProfileItem _snapshot;

    private ProfileEditorModel(ProfileItem snapshot)
    {
        _snapshot = snapshot;
        Remarks = snapshot.Remarks;
        Address = snapshot.Address;
        Port = snapshot.Port;
        Password = snapshot.Password;
        Username = snapshot.Username;
        StreamSecurity = snapshot.StreamSecurity;
        Sni = snapshot.Sni;
        Fingerprint = snapshot.Fingerprint;
        Alpn = snapshot.Alpn;
        EchConfigList = snapshot.EchConfigList;
        VerifyPeerCertByName = snapshot.VerifyPeerCertByName;
        PublicKey = snapshot.PublicKey;
        ShortId = snapshot.ShortId;
        SpiderX = snapshot.SpiderX;
        Mldsa65Verify = snapshot.Mldsa65Verify;
        Finalmask = snapshot.Finalmask;
        DisplayLog = snapshot.DisplayLog;
        PreSocksPort = snapshot.PreSocksPort;
        ConfigType = snapshot.ConfigType;
    }

    [Reactive]
    public partial string Remarks { get; set; }

    [Reactive]
    public partial string Address { get; set; }

    [Reactive]
    public partial int Port { get; set; }

    [Reactive]
    public partial string Password { get; set; }

    [Reactive]
    public partial string Username { get; set; }

    [Reactive]
    public partial string StreamSecurity { get; set; }

    [Reactive]
    public partial string Sni { get; set; }

    [Reactive]
    public partial string Fingerprint { get; set; }

    [Reactive]
    public partial string Alpn { get; set; }

    [Reactive]
    public partial string EchConfigList { get; set; }

    [Reactive]
    public partial string VerifyPeerCertByName { get; set; }

    [Reactive]
    public partial string PublicKey { get; set; }

    [Reactive]
    public partial string ShortId { get; set; }

    [Reactive]
    public partial string SpiderX { get; set; }

    [Reactive]
    public partial string Mldsa65Verify { get; set; }

    [Reactive]
    public partial string Finalmask { get; set; }

    [Reactive]
    public partial bool DisplayLog { get; set; }

    [Reactive]
    public partial int? PreSocksPort { get; set; }

    [Reactive]
    public partial EConfigType ConfigType { get; set; }

    public static ProfileEditorModel FromEntity(ProfileItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var snapshot = JsonUtils.DeepCopy(entity)
            ?? throw new InvalidOperationException("Could not copy ProfileItem for editing.");
        return new ProfileEditorModel(snapshot);
    }

    public ProfileItem ToEntity()
    {
        var entity = JsonUtils.DeepCopy(_snapshot)
            ?? throw new InvalidOperationException("Could not copy ProfileItem for saving.");
        entity.Remarks = Remarks;
        entity.Address = Address;
        entity.Port = Port;
        entity.Password = Password;
        entity.Username = Username;
        entity.StreamSecurity = StreamSecurity;
        entity.Sni = Sni;
        entity.Fingerprint = Fingerprint;
        entity.Alpn = Alpn;
        entity.EchConfigList = EchConfigList;
        entity.VerifyPeerCertByName = VerifyPeerCertByName;
        entity.PublicKey = PublicKey;
        entity.ShortId = ShortId;
        entity.SpiderX = SpiderX;
        entity.Mldsa65Verify = Mldsa65Verify;
        entity.Finalmask = Finalmask;
        entity.DisplayLog = DisplayLog;
        entity.PreSocksPort = PreSocksPort;
        entity.ConfigType = ConfigType;
        return entity;
    }
}
