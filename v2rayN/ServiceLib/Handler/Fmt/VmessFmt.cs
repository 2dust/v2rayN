namespace ServiceLib.Handler.Fmt
{
    public class VmessFmt : BaseFmt
    {
        public static ProfileItem? Resolve(string str, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;
            ProfileItem? item;
            if (str.IndexOf('?') > 0 && str.IndexOf('&') > 0)
            {
                item = ResolveStdVmess(str);
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

            
            VmessQRCode? vmessQRCode = JsonUtils.Deserialize<VmessQRCode>(result);
            if (vmessQRCode == null)
            {
                msg = ResUI.FailedConversionConfiguration;
                return null;
            }

            item.network = Global.DefaultNetwork;
            item.headerType = Global.None;

            item.configVersion = vmessQRCode.v;
            item.remarks = Utils.ToString(vmessQRCode.ps);
            item.address = Utils.ToString(vmessQRCode.add);
            item.port = vmessQRCode.port;
            item.id = Utils.ToString(vmessQRCode.id);
            item.alterId = vmessQRCode.aid;
            item.security = Utils.ToString(vmessQRCode.scy);

            item.security = Utils.IsNotEmpty(vmessQRCode.scy) ? vmessQRCode.scy : Global.DefaultSecurity;
            if (Utils.IsNotEmpty(vmessQRCode.net))
            {
                item.network = vmessQRCode.net;
            }
            if (Utils.IsNotEmpty(vmessQRCode.type))
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

        public static ProfileItem? ResolveStdVmess(string str)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.VMess,
                security = "auto"
            };

            Uri url = new(str);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);

            return item;
        }
    }
}