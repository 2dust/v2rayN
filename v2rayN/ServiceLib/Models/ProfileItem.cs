using ReactiveUI;
using SQLite;

namespace ServiceLib.Models;

[Serializable]
public class ProfileItem: ReactiveObject
{
    public ProfileItem()
    {
        IndexId = string.Empty;
        ConfigType = EConfigType.VMess;
        ConfigVersion = 2;
        Address = string.Empty;
        Port = 0;
        Id = string.Empty;
        AlterId = 0;
        Security = string.Empty;
        Network = string.Empty;
        Remarks = string.Empty;
        HeaderType = string.Empty;
        RequestHost = string.Empty;
        Path = string.Empty;
        StreamSecurity = string.Empty;
        AllowInsecure = string.Empty;
        Subid = string.Empty;
        Flow = string.Empty;
    }

    #region function

    public string GetSummary()
    {
        var summary = $"[{(ConfigType).ToString()}] ";
        var arrAddr = Address.Split('.');
        var addr = arrAddr.Length switch
        {
            > 2 => $"{arrAddr.First()}***{arrAddr.Last()}",
            > 1 => $"***{arrAddr.Last()}",
            _ => Address
        };
        summary += ConfigType switch
        {
            EConfigType.Custom => $"[{CoreType.ToString()}]{Remarks}",
            _ => $"{Remarks}({addr}:{Port})"
        };
        return summary;
    }

    public List<string>? GetAlpn()
    {
        return Alpn.IsNullOrEmpty() ? null : Utils.String2List(Alpn);
    }

    public string GetNetwork()
    {
        if (Network.IsNullOrEmpty() || !Global.Networks.Contains(Network))
        {
            return Global.DefaultNetwork;
        }
        return Network.TrimEx();
    }

    #endregion function

    [PrimaryKey]
    public string IndexId { get; set; }

    public EConfigType ConfigType { get; set; }
    public int ConfigVersion { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string Ports { get; set; }
    public string Id { get; set; }
    public int AlterId { get; set; }
    public string Security { get; set; }
    public string Network { get; set; }
    public string Remarks { get; set; }
    public string HeaderType { get; set; }
    public string RequestHost { get; set; }
    public string Path { get; set; }
    public string StreamSecurity { get; set; }
    public string AllowInsecure { get; set; }
    public string Subid { get; set; }
    public bool IsSub { get; set; } = true;
    public string Flow { get; set; }
    public string Sni { get; set; }
    public string Alpn { get; set; } = string.Empty;
    public ECoreType? CoreType { get; set; }
    public int? PreSocksPort { get; set; }
    public string Fingerprint { get; set; }
    public bool DisplayLog { get; set; } = true;
    public string PublicKey { get; set; }
    public string ShortId { get; set; }
    public string SpiderX { get; set; }
    public string Extra { get; set; }
}
