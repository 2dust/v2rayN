namespace ServiceLib.Handler.Builder;

public record NodeValidatorResult(List<string> Errors, List<string> Warnings)
{
    public bool Success => Errors.Count == 0;

    public static NodeValidatorResult Empty()
    {
        return new NodeValidatorResult([], []);
    }
}

public class NodeValidator
{
    // Static validator rules
    private static readonly HashSet<string> SingboxUnsupportedTransports =
        [nameof(ETransport.kcp), nameof(ETransport.xhttp)];

    private static readonly HashSet<EConfigType> SingboxTransportSupportedProtocols =
        [EConfigType.VMess, EConfigType.VLESS, EConfigType.Trojan, EConfigType.Shadowsocks];

    private static readonly HashSet<string> SingboxShadowsocksAllowedTransports =
        [nameof(ETransport.tcp), nameof(ETransport.ws), nameof(ETransport.quic)];

    public static NodeValidatorResult Validate(ProfileItem item, ECoreType coreType)
    {
        var v = new ValidationContext();
        ValidateNodeAndCoreSupport(item, coreType, v);
        return v.ToResult();
    }

    private class ValidationContext
    {
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];

        public void Error(string message)
        {
            Errors.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Assert(bool condition, string errorMsg)
        {
            if (!condition)
            {
                Error(errorMsg);
            }
        }

        public NodeValidatorResult ToResult()
        {
            return new NodeValidatorResult(Errors, Warnings);
        }
    }

    private static void ValidateNodeAndCoreSupport(ProfileItem item, ECoreType coreType, ValidationContext v)
    {
        if (item.ConfigType is EConfigType.Custom)
        {
            return;
        }

        if (item.ConfigType.IsGroupType())
        {
            // Group logic is handled in ValidateGroupNode
            return;
        }

        // Basic Property Validation
        v.Assert(!item.Address.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, "Address"));
        v.Assert(item.Port is > 0 and <= 65535, string.Format(ResUI.MsgInvalidProperty, "Port"));

        // Network & Core Logic
        var net = item.GetNetwork();
        if (coreType == ECoreType.sing_box)
        {
            var transportError = ValidateSingboxTransport(item.ConfigType, net);
            if (transportError != null)
                v.Error(transportError);

            if (!Global.SingboxSupportConfigType.Contains(item.ConfigType))
            {
                v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, nameof(ECoreType.sing_box), item.ConfigType));
            }
        }
        else if (coreType is ECoreType.Xray)
        {
            if (!Global.XraySupportConfigType.Contains(item.ConfigType))
            {
                v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, nameof(ECoreType.Xray), item.ConfigType));
            }
        }

        // Protocol Specifics
        var protocolExtra = item.GetProtocolExtra();
        switch (item.ConfigType)
        {
            case EConfigType.VMess:
                v.Assert(!item.Password.IsNullOrEmpty() && Utils.IsGuidByParse(item.Password),
                    string.Format(ResUI.MsgInvalidProperty, "Password"));
                break;

            case EConfigType.VLESS:
                v.Assert(
                    !item.Password.IsNullOrEmpty()
                    && (Utils.IsGuidByParse(item.Password) || item.Password.Length <= 30),
                    string.Format(ResUI.MsgInvalidProperty, "Password")
                );
                v.Assert(Global.Flows.Contains(protocolExtra.Flow ?? string.Empty),
                    string.Format(ResUI.MsgInvalidProperty, "Flow"));
                break;

            case EConfigType.Shadowsocks:
                v.Assert(!item.Password.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, "Password"));
                v.Assert(
                    !string.IsNullOrEmpty(protocolExtra.SsMethod) &&
                    Global.SsSecuritiesInSingbox.Contains(protocolExtra.SsMethod),
                    string.Format(ResUI.MsgInvalidProperty, "SsMethod"));
                break;
        }

        // TLS & Security
        if (item.StreamSecurity == Global.StreamSecurity)
        {
            if (!item.Cert.IsNullOrEmpty() && CertPemManager.ParsePemChain(item.Cert).Count == 0 &&
                !item.CertSha.IsNullOrEmpty())
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, "TLS Certificate"));
            }
        }

        if (item.StreamSecurity == Global.StreamSecurityReality)
        {
            v.Assert(!item.PublicKey.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, "PublicKey"));
        }

        if (item.Network == nameof(ETransport.xhttp) && !item.Extra.IsNullOrEmpty())
        {
            if (JsonUtils.ParseJson(item.Extra) is null)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, "XHTTP Extra"));
            }
        }
    }

    private static string? ValidateSingboxTransport(EConfigType configType, string net)
    {
        // sing-box does not support xhttp / kcp transports
        if (SingboxUnsupportedTransports.Contains(net))
        {
            return string.Format(ResUI.MsgCoreNotSupportNetwork, nameof(ECoreType.sing_box), net);
        }

        // sing-box does not support non-tcp transports for protocols other than vmess/trojan/vless/shadowsocks
        if (!SingboxTransportSupportedProtocols.Contains(configType) && net != nameof(ETransport.tcp))
        {
            return string.Format(ResUI.MsgCoreNotSupportProtocolTransport,
                nameof(ECoreType.sing_box), configType.ToString(), net);
        }

        // sing-box shadowsocks only supports tcp/ws/quic transports
        if (configType == EConfigType.Shadowsocks && !SingboxShadowsocksAllowedTransports.Contains(net))
        {
            return string.Format(ResUI.MsgCoreNotSupportProtocolTransport,
                nameof(ECoreType.sing_box), configType.ToString(), net);
        }

        return null;
    }
}
