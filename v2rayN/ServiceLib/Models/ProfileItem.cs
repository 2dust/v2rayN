namespace ServiceLib.Models;

[Serializable]
public class ProfileItem : ReactiveObject
{
    private ProtocolExtraItem _protocolExtraItem = new();

    public ProfileItem()
    {
        IndexId = string.Empty;
        ConfigType = EConfigType.VMess;
        ConfigVersion = 3;
        Address = string.Empty;
        Port = 0;
        Password = string.Empty;
        Network = string.Empty;
        Remarks = string.Empty;
        HeaderType = string.Empty;
        RequestHost = string.Empty;
        Path = string.Empty;
        StreamSecurity = string.Empty;
        AllowInsecure = string.Empty;
        Subid = string.Empty;
    }

    #region function

    public string GetSummary()
    {
        var summary = $"[{ConfigType.ToString()}] ";
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
        return ConfigType.IsComplexType();
    }

    public bool IsValid()
    {
        if (IsComplex())
        {
            return true;
        }

        if (Address.IsNullOrEmpty() || Port is <= 0 or >= 65536)
        {
            return false;
        }

        switch (ConfigType)
        {
            case EConfigType.VMess:
                if (Password.IsNullOrEmpty() || !Utils.IsGuidByParse(Password))
                {
                    return false;
                }

                break;

            case EConfigType.VLESS:
                if (Password.IsNullOrEmpty() || (!Utils.IsGuidByParse(Password) && Password.Length > 30))
                {
                    return false;
                }

                if (!Global.Flows.Contains(GetProtocolExtra().Flow ?? string.Empty))
                {
                    return false;
                }

                break;

            case EConfigType.Shadowsocks:
                if (Password.IsNullOrEmpty())
                {
                    return false;
                }

                var protocolExtra = GetProtocolExtra();

                if (string.IsNullOrEmpty(protocolExtra.SsMethod)
                    || !Global.SsSecuritiesInSingbox.Contains(protocolExtra.SsMethod))
                {
                    return false;
                }

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

    public void SetProtocolExtra(ProtocolExtraItem extraItem)
    {
        _protocolExtraItem = extraItem;
    }

    public ProtocolExtraItem GetProtocolExtra()
    {
        return _protocolExtraItem;
    }

    #endregion function

    [PrimaryKey]
    public string IndexId { get; set; }

    public EConfigType ConfigType { get; set; }
    public int ConfigVersion { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string Password { get; set; }
    public string Network { get; set; }
    public string Remarks { get; set; }
    public string HeaderType { get; set; }
    public string RequestHost { get; set; }
    public string Path { get; set; }
    public string StreamSecurity { get; set; }
    public string AllowInsecure { get; set; }
    public string Subid { get; set; }
    public bool IsSub { get; set; } = true;
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
    public string Cert { get; set; }
    public string CertSha { get; set; }
    public string EchConfigList { get; set; }
    public string EchForceQuery { get; set; }

    public string ProtoExtra
    {
        get => JsonUtils.Serialize(_protocolExtraItem, false);
        set => _protocolExtraItem = JsonUtils.Deserialize<ProtocolExtraItem>(value);
    }

    [Obsolete("Use ProtocolExtraItem.Ports instead.")]
    public string Ports { get; set; }
    [Obsolete("Use ProtocolExtraItem.AlterId instead.")]
    public int AlterId { get; set; }
    [Obsolete("Use ProtocolExtraItem.Flow instead.")]
    public string Flow { get; set; }
    [Obsolete("Use ProfileItem.Password instead.")]
    public string Id { get; set; }
    [Obsolete("Use ProtocolExtraItem.xxx instead.")]
    public string Security { get; set; }
}
