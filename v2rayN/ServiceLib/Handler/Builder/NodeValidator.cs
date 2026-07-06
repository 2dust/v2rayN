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
        [nameof(ETransport.raw), nameof(ETransport.ws)];

    public static NodeValidatorResult Validate(ProfileItem item, ECoreType coreType)
    {
        var v = new ValidationContext();
        ValidateNodeAndCoreSupport(item, coreType, v);
        return v.ToResult();
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
        v.Assert(!item.Address.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, ResUI.TbAddress));
        v.Assert(item.Port is > 0 and <= 65535, string.Format(ResUI.MsgInvalidProperty, ResUI.TbPort));

        // Network & Core Logic
        var net = item.GetNetwork();
        if (coreType == ECoreType.sing_box)
        {
            var transportError = ValidateSingboxTransport(item.ConfigType, net);
            if (transportError != null)
            {
                v.Error(transportError);
            }

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
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbId));
                break;

            case EConfigType.VLESS:
                v.Assert(
                    !item.Password.IsNullOrEmpty()
                    && (Utils.IsGuidByParse(item.Password) || item.Password.Length <= 30),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbId5)
                );
                v.Assert(Global.Flows.Contains(protocolExtra.Flow ?? string.Empty),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbFlow5));
                break;

            case EConfigType.Shadowsocks:
                v.Assert(!item.Password.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, ResUI.TbId3));
                v.Assert(
                    !string.IsNullOrEmpty(protocolExtra.SsMethod) &&
                    Global.SsSecuritiesInSingbox.Contains(protocolExtra.SsMethod),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbSecurity3));
                break;
        }

        if (coreType is ECoreType.Xray
            && (protocolExtra.Flow ?? string.Empty).StartsWith("xtls", StringComparison.OrdinalIgnoreCase)
            && item.MuxEnabled == true)
        {
            v.Warning(string.Format(ResUI.MsgOptionsConflict, "XTLS", "Mux.Cool"));
        }

        if (item.GetNetwork() is nameof(ETransport.ws)
            && item.EchConfigList.IsNullOrEmpty()
            && item.GetAlpn()?.FirstOrDefault() == "h3")
        {
            v.Warning(
                "WebSocket but ALPN is set to h3, the core may ignore the ALPN setting or cause unexpected issues.");
        }

        // TLS 与安全性验证
        // 注意：Xray v26.2.6+（2026.6.1 起）已完全移除 allowInsecure 字段。
        // 继续使用该字段会导致 Xray 核心拒绝启动。
        // 迁移方案：
        //   - 已知证书 SHA256 → 使用 pinnedPeerCertSha256（UI 中「证书 SHA256」字段）
        //   - 已知完整证书 PEM → 使用 certificates + disableSystemRoot
        //   - sing-box：继续使用 insecure=true（sing-box 仍支持，保持兼容）
        if (item.StreamSecurity == Global.StreamSecurity)
        {
            var isCertProvided = !item.Cert.IsNullOrEmpty();
            if (!item.Cert.IsNullOrEmpty() && CertPemManager.ParsePemChain(item.Cert).Count == 0)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, ResUI.TbFullCertTips));
                isCertProvided = false;
            }

            // 当 Xray 核心使用 allowInsecure=true 且未提供任何证书固定方式时，
            // 这在 Xray v26.2.6+ 中会导致核心拒绝运行，升级为错误。
            if (coreType == ECoreType.Xray && item.GetAllowInsecure() && !isCertProvided)
            {
                if (item.CertSha.IsNullOrEmpty())
                {
                    // 无任何证书固定：直接报错，并提示迁移方式
                    v.Error(ResUI.MsgAllowInsecureRemovedXray);
                }
                else
                {
                    // 已有 CertSha：自动迁移到 pinnedPeerCertSha256，降为警告
                    v.Warning(ResUI.MsgAllowInsecureAutoMigratedXray);
                }
            }

            // sing-box 的 insecure 字段仍受支持，保持原有警告逻辑（兼容性保留）
            if (coreType == ECoreType.sing_box && item.GetAllowInsecure() && !isCertProvided)
            {
                v.Warning(ResUI.MsgInsecureConfiguration);
            }
        }

        if (item.StreamSecurity == Global.StreamSecurityReality)
        {
            v.Assert(!item.PublicKey.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, ResUI.TbPublicKey));
        }

        var transport = item.GetTransportExtra();
        if (item.Network == nameof(ETransport.xhttp) && !transport.XhttpExtra.IsNullOrEmpty())
        {
            if (JsonUtils.ParseJson(transport.XhttpExtra) is not JsonObject)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, ResUI.TransportExtra));
            }
        }

        if (!item.Finalmask.IsNullOrEmpty())
        {
            if (JsonUtils.ParseJson(item.Finalmask) is not JsonObject)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, ResUI.TbFinalmask));
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
        if (!SingboxTransportSupportedProtocols.Contains(configType) && net != nameof(ETransport.raw))
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
}
