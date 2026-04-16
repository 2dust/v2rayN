namespace ServiceLib.Models;

[Serializable]
public class ProfileItem
{
    private ProtocolExtraItem? _protocolExtraCache;
    private TransportExtraItem? _transportExtraCache;

    public ProfileItem()
    {
        IndexId = string.Empty;
        ConfigType = EConfigType.VMess;
        ConfigVersion = 4;
        Subid = string.Empty;
        Address = string.Empty;
        Port = 0;
        Password = string.Empty;
        Username = string.Empty;
        Network = string.Empty;
        Remarks = string.Empty;
        StreamSecurity = string.Empty;
        AllowInsecure = string.Empty;
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

                if (string.IsNullOrEmpty(GetProtocolExtra().SsMethod)
                    || !Global.SsSecuritiesInSingbox.Contains(GetProtocolExtra().SsMethod))
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

    public ProtocolExtraItem GetProtocolExtra()
    {
        return _protocolExtraCache ??= JsonUtils.Deserialize<ProtocolExtraItem>(ProtoExtra) ?? new ProtocolExtraItem();
    }

    public void SetProtocolExtra(ProtocolExtraItem extraItem)
    {
        _protocolExtraCache = extraItem;
        ProtoExtra = JsonUtils.Serialize(extraItem, false);
    }

    public TransportExtraItem GetTransportExtra()
    {
        return _transportExtraCache ??= JsonUtils.Deserialize<TransportExtraItem>(TransportExtra) ?? new TransportExtraItem();
    }

    public void SetTransportExtra(TransportExtraItem transportExtra)
    {
        _transportExtraCache = transportExtra;
        TransportExtra = JsonUtils.Serialize(transportExtra, false);
    }

    #endregion function

    [PrimaryKey]
    public string IndexId { get; set; }

    public EConfigType ConfigType { get; set; }
    public ECoreType? CoreType { get; set; }
    public int ConfigVersion { get; set; }
    public string Subid { get; set; }
    public bool IsSub { get; set; } = true;
    public int? PreSocksPort { get; set; }
    public bool DisplayLog { get; set; } = true;
    public string Remarks { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string Password { get; set; }
    public string Username { get; set; }
    public string Network { get; set; }

    [Obsolete("Use TransportExtra.RawHeaderType/XhttpMode/GrpcMode/KcpHeaderType instead.")]
    public string HeaderType { get; set; }

    [Obsolete("Use TransportExtra.Host/GrpcAuthority instead.")]
    public string RequestHost { get; set; }

    [Obsolete("Use TransportExtra.Path/GrpcServiceName/KcpSeed instead.")]
    public string Path { get; set; }

    public string StreamSecurity { get; set; }
    public string AllowInsecure { get; set; }
    public string Sni { get; set; }
    public string Alpn { get; set; } = string.Empty;
    public string Fingerprint { get; set; }
    public string PublicKey { get; set; }
    public string ShortId { get; set; }
    public string SpiderX { get; set; }
    public string Mldsa65Verify { get; set; }

    [Obsolete("Use TransportExtra.XhttpExtra instead.")]
    public string Extra { get; set; }

    public bool? MuxEnabled { get; set; }
    public string Cert { get; set; }
    public string CertSha { get; set; }
    public string EchConfigList { get; set; }
    public string EchForceQuery { get; set; }
    public string Finalmask { get; set; }

    public string ProtoExtra { get; set; }
    public string TransportExtra { get; set; }

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
