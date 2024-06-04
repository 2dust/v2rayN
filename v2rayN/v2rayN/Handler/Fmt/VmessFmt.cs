using System.Text.RegularExpressions;
using v2rayN.Enums;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.Handler.Fmt
{
    internal class VmessFmt : BaseFmt
    {
        private static readonly Regex StdVmessUserInfo = new(
            @"^(?<network>[a-z]+)(\+(?<streamSecurity>[a-z]+))?:(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$", RegexOptions.Compiled);

        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;
            ProfileItem? item;
            int indexSplit = str.IndexOf("?");
            if (indexSplit > 0)
            {
                item = ResolveStdVmess(str) ?? ResolveVmess4Kitsunebi(str);
            }
            else
            {
                item = ResolveVmess(str, out msg);
            }
            return item;
        }

        public static string? ToUri(ProfileItem? item)
        {
            if (item == null) return null;
            string url = string.Empty;

            VmessQRCode vmessQRCode = new()
            {
                v = item.configVersion,
                ps = item.remarks.TrimEx(),
                add = item.address,
                port = item.port,
                id = item.id,
                aid = item.alterId,
                scy = item.security,
                net = item.network,
                type = item.headerType,
                host = item.requestHost,
                path = item.path,
                tls = item.streamSecurity,
                sni = item.sni,
                alpn = item.alpn,
                fp = item.fingerprint
            };

            url = JsonUtils.Serialize(vmessQRCode);
            url = Utils.Base64Encode(url);
            url = $"{Global.ProtocolShares[EConfigType.VMess]}{url}";

            return url;
        }

        private static ProfileItem? ResolveVmess(string result, out string msg)
        {
            msg = string.Empty;
            var item = new ProfileItem
            {
                configType = EConfigType.VMess
            };

            result = result[Global.ProtocolShares[EConfigType.VMess].Length..];
            result = Utils.Base64Decode(result);

            //转成Json
            VmessQRCode? vmessQRCode = JsonUtils.Deserialize<VmessQRCode>(result);
            if (vmessQRCode == null)
            {
                msg = ResUI.FailedConversionConfiguration;
                return null;
            }

            item.network = Global.DefaultNetwork;
            item.headerType = Global.None;

            item.configVersion = Utils.ToInt(vmessQRCode.v);
            item.remarks = Utils.ToString(vmessQRCode.ps);
            item.address = Utils.ToString(vmessQRCode.add);
            item.port = Utils.ToInt(vmessQRCode.port);
            item.id = Utils.ToString(vmessQRCode.id);
            item.alterId = Utils.ToInt(vmessQRCode.aid);
            item.security = Utils.ToString(vmessQRCode.scy);

            item.security = !Utils.IsNullOrEmpty(vmessQRCode.scy) ? vmessQRCode.scy : Global.DefaultSecurity;
            if (!Utils.IsNullOrEmpty(vmessQRCode.net))
            {
                item.network = vmessQRCode.net;
            }
            if (!Utils.IsNullOrEmpty(vmessQRCode.type))
            {
                item.headerType = vmessQRCode.type;
            }

            item.requestHost = Utils.ToString(vmessQRCode.host);
            item.path = Utils.ToString(vmessQRCode.path);
            item.streamSecurity = Utils.ToString(vmessQRCode.tls);
            item.sni = Utils.ToString(vmessQRCode.sni);
            item.alpn = Utils.ToString(vmessQRCode.alpn);
            item.fingerprint = Utils.ToString(vmessQRCode.fp);

            return item;
        }

        private static ProfileItem? ResolveStdVmess(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.VMess,
                security = "auto"
            };

            Uri u = new(result);

            item.address = u.IdnHost;
            item.port = u.Port;
            item.remarks = u.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            var query = Utils.ParseQueryString(u.Query);

            var m = StdVmessUserInfo.Match(u.UserInfo);
            if (!m.Success) return null;

            item.id = m.Groups["id"].Value;

            if (m.Groups["streamSecurity"].Success)
            {
                item.streamSecurity = m.Groups["streamSecurity"].Value;
            }
            switch (item.streamSecurity)
            {
                case Global.StreamSecurity:
                    break;

                default:
                    if (!Utils.IsNullOrEmpty(item.streamSecurity))
                        return null;
                    break;
            }

            item.network = m.Groups["network"].Value;
            switch (item.network)
            {
                case nameof(ETransport.tcp):
                    string t1 = query["type"] ?? Global.None;
                    item.headerType = t1;
                    break;

                case nameof(ETransport.kcp):
                    item.headerType = query["type"] ?? Global.None;
                    break;

                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                    string p1 = query["path"] ?? "/";
                    string h1 = query["host"] ?? "";
                    item.requestHost = Utils.UrlDecode(h1);
                    item.path = p1;
                    break;

                case nameof(ETransport.http):
                case nameof(ETransport.h2):
                    item.network = nameof(ETransport.h2);
                    string p2 = query["path"] ?? "/";
                    string h2 = query["host"] ?? "";
                    item.requestHost = Utils.UrlDecode(h2);
                    item.path = p2;
                    break;

                case nameof(ETransport.quic):
                    string s = query["security"] ?? Global.None;
                    string k = query["key"] ?? "";
                    string t3 = query["type"] ?? Global.None;
                    item.headerType = t3;
                    item.requestHost = Utils.UrlDecode(s);
                    item.path = k;
                    break;

                default:
                    return null;
            }

            return item;
        }

        private static ProfileItem? ResolveVmess4Kitsunebi(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.VMess
            };
            result = result[Global.ProtocolShares[EConfigType.VMess].Length..];
            int indexSplit = result.IndexOf("?");
            if (indexSplit > 0)
            {
                result = result[..indexSplit];
            }
            result = Utils.Base64Decode(result);

            string[] arr1 = result.Split('@');
            if (arr1.Length != 2)
            {
                return null;
            }
            string[] arr21 = arr1[0].Split(':');
            string[] arr22 = arr1[1].Split(':');
            if (arr21.Length != 2 || arr22.Length != 2)
            {
                return null;
            }

            item.address = arr22[0];
            item.port = Utils.ToInt(arr22[1]);
            item.security = arr21[0];
            item.id = arr21[1];

            item.network = Global.DefaultNetwork;
            item.headerType = Global.None;
            item.remarks = "Alien";

            return item;
        }
    }
}