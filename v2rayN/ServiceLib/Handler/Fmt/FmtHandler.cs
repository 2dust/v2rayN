﻿namespace ServiceLib.Handler.Fmt
{
    public class FmtHandler
    {
        public static string? GetShareUri(ProfileItem item)
        {
            try
            {
                var url = item.configType switch
                {
                    EConfigType.VMess => VmessFmt.ToUri(item),
                    EConfigType.Shadowsocks => ShadowsocksFmt.ToUri(item),
                    EConfigType.SOCKS => SocksFmt.ToUri(item),
                    EConfigType.Trojan => TrojanFmt.ToUri(item),
                    EConfigType.VLESS => VLESSFmt.ToUri(item),
                    EConfigType.Hysteria2 => Hysteria2Fmt.ToUri(item),
                    EConfigType.TUIC => TuicFmt.ToUri(item),
                    EConfigType.WireGuard => WireguardFmt.ToUri(item),
                    _ => null,
                };

                return url;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return "";
            }
        }

        public static ProfileItem? ResolveConfig(string config, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;

            try
            {
                string str = config.TrimEx();
                if (Utils.IsNullOrEmpty(str))
                {
                    msg = ResUI.FailedReadConfiguration;
                    return null;
                }

                if (str.StartsWith(Global.ProtocolShares[EConfigType.VMess]))
                {
                    return VmessFmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.Shadowsocks]))
                {
                    return ShadowsocksFmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.SOCKS]))
                {
                    return SocksFmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.Trojan]))
                {
                    return TrojanFmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.VLESS]))
                {
                    return VLESSFmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.Hysteria2]) || str.StartsWith(Global.Hysteria2ProtocolShare))
                {
                    return Hysteria2Fmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.TUIC]))
                {
                    return TuicFmt.Resolve(str, out msg);
                }
                else if (str.StartsWith(Global.ProtocolShares[EConfigType.WireGuard]))
                {
                    return WireguardFmt.Resolve(str, out msg);
                }
                else
                {
                    msg = ResUI.NonvmessOrssProtocol;
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                msg = ResUI.Incorrectconfiguration;
                return null;
            }
        }
    }
}