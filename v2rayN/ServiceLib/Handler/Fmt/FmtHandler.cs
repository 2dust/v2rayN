namespace ServiceLib.Handler.Fmt;

public class FmtHandler
{
    private static readonly string _tag = "FmtHandler";

    public static string? GetShareUri(ProfileItem item)
    {
        try
        {
            var url = item.ConfigType switch
            {
                EConfigType.VMess => VmessFmt.ToUri(item),
                EConfigType.Shadowsocks => ShadowsocksFmt.ToUri(item),
                EConfigType.SOCKS => SocksFmt.ToUri(item),
                EConfigType.Trojan => TrojanFmt.ToUri(item),
                EConfigType.VLESS => VLESSFmt.ToUri(item),
                EConfigType.Hysteria2 => Hysteria2Fmt.ToUri(item),
                EConfigType.TUIC => TuicFmt.ToUri(item),
                EConfigType.WireGuard => WireguardFmt.ToUri(item),
                EConfigType.Anytls => AnytlsFmt.ToUri(item),
                _ => null,
            };

            return url;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return string.Empty;
        }
    }

    public static ProfileItem? ResolveConfig(string config, out string msg)
    {
        var span = config.AsSpan().Trim();
        if (span.IsEmpty)
        {
            msg = ResUI.FailedReadConfiguration;
            return null;
        }

        try
        {
            return span switch
            {
                _ when IsProtocol(span, EConfigType.VMess) => VmessFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.Shadowsocks) => ShadowsocksFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.SOCKS) => SocksFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.Trojan) => TrojanFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.VLESS) => VLESSFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.Hysteria2) || span.StartsWith(AppConfig.Hysteria2ProtocolShare,
                    StringComparison.OrdinalIgnoreCase) => Hysteria2Fmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.TUIC) => TuicFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.WireGuard) => WireguardFmt.Resolve(config, out msg),
                _ when IsProtocol(span, EConfigType.Anytls) => AnytlsFmt.Resolve(config, out msg),
                _ => HandleUnknown(out msg)
            };
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            msg = ResUI.Incorrectconfiguration;
            return null;
        }
    }

    private static bool IsProtocol(ReadOnlySpan<char> strSpan, EConfigType type)
    {
        var prefix = AppConfig.ProtocolShares[type].AsSpan();
        return strSpan.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static ProfileItem? HandleUnknown(out string msg)
    {
        msg = ResUI.NonvmessOrssProtocol;
        return null;
    }
}
