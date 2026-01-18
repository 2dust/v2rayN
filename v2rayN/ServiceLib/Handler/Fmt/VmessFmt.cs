namespace ServiceLib.Handler.Fmt;

public class VmessFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        ProfileItem? item;
        if (str.IndexOf('@') > 0)
        {
            item = ResolveStdVmess(str) ?? ResolveVmess(str, out msg);
        }
        else
        {
            item = ResolveVmess(str, out msg);
        }
        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var extraItem = item?.GetExtraItem();
        var vmessQRCode = new VmessQRCode
        {
            v = item.ConfigVersion,
            ps = item.Remarks.TrimEx(),
            add = item.Address,
            port = item.Port,
            id = item.Id,
            aid = int.TryParse(extraItem?.AlterId, out var result) ? result : 0,
            scy = item.Security,
            net = item.Network,
            type = item.HeaderType,
            host = item.RequestHost,
            path = item.Path,
            tls = item.StreamSecurity,
            sni = item.Sni,
            alpn = item.Alpn,
            fp = item.Fingerprint,
            insecure = item.AllowInsecure.Equals(Global.AllowInsecure.First()) ? "1" : "0"
        };

        var url = JsonUtils.Serialize(vmessQRCode);
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

        var vmessQRCode = JsonUtils.Deserialize<VmessQRCode>(result);
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
        item.SetExtraItem(new ProtocolExtraItem
        {
            AlterId = vmessQRCode.aid.ToString(),
        });
        item.Security = Utils.ToString(vmessQRCode.scy);

        item.Security = vmessQRCode.scy.IsNotEmpty() ? vmessQRCode.scy : Global.DefaultSecurity;
        if (vmessQRCode.net.IsNotEmpty())
        {
            item.Network = vmessQRCode.net;
        }
        if (vmessQRCode.type.IsNotEmpty())
        {
            item.HeaderType = vmessQRCode.type;
        }

        item.RequestHost = Utils.ToString(vmessQRCode.host);
        item.Path = Utils.ToString(vmessQRCode.path);
        item.StreamSecurity = Utils.ToString(vmessQRCode.tls);
        item.Sni = Utils.ToString(vmessQRCode.sni);
        item.Alpn = Utils.ToString(vmessQRCode.alpn);
        item.Fingerprint = Utils.ToString(vmessQRCode.fp);
        item.AllowInsecure = vmessQRCode.insecure == "1" ? Global.AllowInsecure.First() : string.Empty;

        return item;
    }

    public static ProfileItem? ResolveStdVmess(string str)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VMess,
            Security = "auto"
        };

        var url = Utils.TryUri(str);
        if (url == null)
        {
            return null;
        }

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        item.Id = Utils.UrlDecode(url.UserInfo);

        var query = Utils.ParseQueryString(url.Query);
        ResolveUriQuery(query, ref item);

        return item;
    }
}
