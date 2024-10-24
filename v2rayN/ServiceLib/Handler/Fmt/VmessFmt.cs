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
                v = item.ConfigVersion,
                ps = item.Remarks.TrimEx(),
                add = item.Address,
                port = item.Port,
                id = item.Id,
                aid = item.AlterId,
                scy = item.Security,
                net = item.Network,
                type = item.HeaderType,
                host = item.RequestHost,
                path = item.Path,
                tls = item.StreamSecurity,
                sni = item.Sni,
                alpn = item.Alpn,
                fp = item.Fingerprint
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
                ConfigType = EConfigType.VMess
            };

            result = result[Global.ProtocolShares[EConfigType.VMess].Length..];
            result = Utils.Base64Decode(result);

            VmessQRCode? vmessQRCode = JsonUtils.Deserialize<VmessQRCode>(result);
            if (vmessQRCode == null)
            {
                msg = ResUI.FailedConversionConfiguration;
                return null;
            }

            item.Network = Global.DefaultNetwork;
            item.HeaderType = Global.None;

            item.ConfigVersion = vmessQRCode.v;
            item.Remarks = Utils.ToString(vmessQRCode.ps);
            item.Address = Utils.ToString(vmessQRCode.add);
            item.Port = vmessQRCode.port;
            item.Id = Utils.ToString(vmessQRCode.id);
            item.AlterId = vmessQRCode.aid;
            item.Security = Utils.ToString(vmessQRCode.scy);

            item.Security = Utils.IsNotEmpty(vmessQRCode.scy) ? vmessQRCode.scy : Global.DefaultSecurity;
            if (Utils.IsNotEmpty(vmessQRCode.net))
            {
                item.Network = vmessQRCode.net;
            }
            if (Utils.IsNotEmpty(vmessQRCode.type))
            {
                item.HeaderType = vmessQRCode.type;
            }

            item.RequestHost = Utils.ToString(vmessQRCode.host);
            item.Path = Utils.ToString(vmessQRCode.path);
            item.StreamSecurity = Utils.ToString(vmessQRCode.tls);
            item.Sni = Utils.ToString(vmessQRCode.sni);
            item.Alpn = Utils.ToString(vmessQRCode.alpn);
            item.Fingerprint = Utils.ToString(vmessQRCode.fp);

            return item;
        }

        public static ProfileItem? ResolveStdVmess(string str)
        {
            ProfileItem item = new()
            {
                ConfigType = EConfigType.VMess,
                Security = "auto"
            };

            var url = Utils.TryUri(str);
            if (url == null) return null;

            item.Address = url.IdnHost;
            item.Port = url.Port;
            item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.Id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);

            return item;
        }
    }
}