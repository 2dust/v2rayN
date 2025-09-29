using ReactiveUI;
using SQLite;

namespace ServiceLib.Models;

[Serializable]
public class ProfileItem : ReactiveObject
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
        if (IsComplex())
        {
            summary += $"[{CoreType.ToString()}]{Remarks}";
        }
        else
        {
            var arrAddr = Address.Contains(':') ? Address.Split(':') : Address.Split('.');
            var addr = arrAddr.Length switch
            {
                > 2 => $"{arrAddr.First()}***{arrAddr.Last()}",
                > 1 => $"***{arrAddr.Last()}",
                _ => Address
            };
            summary += $"{Remarks}({addr}:{Port})";
        }
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

    public bool IsComplex()
    {
        return ConfigType is EConfigType.Custom or > EConfigType.Group;
    }

    public bool IsValid()
    {
        if (IsComplex())
            return true;

        if (Address.IsNullOrEmpty() || Port is <= 0 or >= 65536)
            return false;

        switch (ConfigType)
        {
            case EConfigType.VMess:
                if (Id.IsNullOrEmpty() || !Utils.IsGuidByParse(Id))
                    return false;
                break;

            case EConfigType.VLESS:
                if (Id.IsNullOrEmpty() || (!Utils.IsGuidByParse(Id) && Id.Length > 30))
                    return false;
                if (!Global.Flows.Contains(Flow))
                    return false;
                break;

            case EConfigType.Shadowsocks:
                if (Id.IsNullOrEmpty())
                    return false;
                if (string.IsNullOrEmpty(Security) || !Global.SsSecuritiesInSingbox.Contains(Security))
                    return false;
                break;
        }

        if ((ConfigType is EConfigType.VLESS or EConfigType.Trojan)
            && StreamSecurity == Global.StreamSecurityReality
            && PublicKey.IsNullOrEmpty())
        {
            return false;
        }

        return true;
    }

    public async Task<bool> HasCycle(HashSet<string> visited, HashSet<string> stack)
    {
        if (ConfigType < EConfigType.Group)
            return false;

        if (stack.Contains(IndexId))
            return true;

        if (visited.Contains(IndexId))
            return false;

        visited.Add(IndexId);
        stack.Add(IndexId);

        if (ProfileGroupItemManager.Instance.TryGet(IndexId, out var group)
            && !group.ChildItems.IsNullOrEmpty())
        {
            var childProfiles = (await Task.WhenAll(
                    Utils.String2List(group.ChildItems)
                        .Where(p => !p.IsNullOrEmpty())
                        .Select(AppManager.Instance.GetProfileItem)
                ))
                .Where(p => p != null)
                .ToList();

            foreach (var child in childProfiles)
            {
                if (await child.HasCycle(visited, stack))
                    return true;
            }
        }

        stack.Remove(IndexId);
        return false;
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
    public string Mldsa65Verify { get; set; }
    public string Extra { get; set; }
    public bool? MuxEnabled { get; set; }
}
