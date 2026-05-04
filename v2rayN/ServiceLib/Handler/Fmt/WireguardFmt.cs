namespace ServiceLib.Handler.Fmt;

public class WireguardFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        ProfileItem item = new()
        {
            ConfigType = EConfigType.WireGuard
        };

        var url = Utils.TryUri(str);
        if (url == null)
        {
            return null;
        }

        item.Address = url.IdnHost;
        item.Port = url.Port;
        item.Remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        item.Password = Utils.UrlDecode(url.UserInfo);

        var query = Utils.ParseQueryString(url.Query);

        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            WgPublicKey = GetQueryDecoded(query, "publickey"),
            WgPresharedKey = GetQueryDecoded(query, "presharedkey"),
            WgReserved = GetQueryDecoded(query, "reserved"),
            WgInterfaceAddress = GetQueryDecoded(query, "address"),
            WgMtu = int.TryParse(GetQueryDecoded(query, "mtu"), out var mtuVal) ? mtuVal : null,
        });

        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var remark = string.Empty;
        if (item.Remarks.IsNotEmpty())
        {
            remark = "#" + Utils.UrlEncode(item.Remarks);
        }

        var protoExtra = item.GetProtocolExtra();
        var dicQuery = new Dictionary<string, string>();
        if (!protoExtra.WgPublicKey.IsNullOrEmpty())
        {
            dicQuery.Add("publickey", Utils.UrlEncode(protoExtra.WgPublicKey));
        }
        if (!protoExtra.WgPresharedKey.IsNullOrEmpty())
        {
            dicQuery.Add("presharedkey", Utils.UrlEncode(protoExtra.WgPresharedKey));
        }
        if (!protoExtra.WgReserved.IsNullOrEmpty())
        {
            dicQuery.Add("reserved", Utils.UrlEncode(protoExtra.WgReserved));
        }
        if (!protoExtra.WgInterfaceAddress.IsNullOrEmpty())
        {
            dicQuery.Add("address", Utils.UrlEncode(protoExtra.WgInterfaceAddress));
        }
        if (protoExtra.WgMtu > 0)
        {
            dicQuery.Add("mtu", protoExtra.WgMtu.ToString());
        }
        return ToUri(EConfigType.WireGuard, item.Address, item.Port, item.Password, dicQuery, remark);
    }

    public static List<ProfileItem>? ResolveConfig(string strData)
    {
        //var dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var interfaceDic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var peerDicList = new List<Dictionary<string, string>>();
        var currentDicRef = interfaceDic;
        using (var reader = new StringReader(strData))
        {
            while (reader.ReadLine() is { } line)
            {
                if (line.IsNullOrEmpty())
                {
                    continue;
                }

                var trimmedLine = line.Trim();

                if (trimmedLine.Equals("[Interface]", StringComparison.OrdinalIgnoreCase))
                {
                    currentDicRef = interfaceDic;
                    continue;
                }
                if (trimmedLine.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                {
                    var peerDic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    peerDicList.Add(peerDic);
                    currentDicRef = peerDic;
                    continue;
                }

                if (trimmedLine.StartsWith('[') || trimmedLine.StartsWith('#') || trimmedLine.StartsWith(';'))
                {
                    continue;
                }

                var idx = line.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();
                // Remove inline comments starting with # or ;
                var commentPos = -1;
                var commentChars = new[] { '#', ';' };
                foreach (var cc in commentChars)
                {
                    var p = value.IndexOf(cc);
                    if (p >= 0 && (commentPos < 0 || p < commentPos))
                    {
                        commentPos = p;
                    }
                }
                if (commentPos >= 0)
                {
                    value = value[..commentPos].TrimEnd();
                }

                currentDicRef[key] = value;
            }
        }

        if (!interfaceDic.TryGetValue("PrivateKey", out var privateKey) || privateKey.IsNullOrEmpty())
        {
            return null;
        }

        var wgMtu = interfaceDic.TryGetValue("MTU", out var mtuStr) && int.TryParse(mtuStr, out var mtuVal) ? mtuVal : 0;
        var wgInterfaceAddress = interfaceDic.TryGetValue("Address", out var interfaceAddress) ? interfaceAddress : string.Empty;

        var index = 0;
        var resultList = new List<ProfileItem>();

        foreach (var peerDic in peerDicList)
        {
            if (!peerDic.TryGetValue("Endpoint", out var endpoint) || endpoint.IsNullOrEmpty())
            {
                continue;
            }

            var endpointParts = endpoint.Split(':');
            var peerAddress = endpointParts[0].Trim();
            if (peerAddress.StartsWith('[')) // IPv6 address
            {
                peerAddress = peerAddress.Trim('[', ']');
            }
            var peerPort = endpointParts.Length > 1 && int.TryParse(endpointParts[1].Trim(), out var portVal) && portVal is > 0 and <= 65535 ? portVal : 2408;

            var protoExtra = new ProtocolExtraItem
            {
                WgPublicKey = (peerDic.TryGetValue("PublicKey", out var publicKey) ? publicKey : string.Empty).NullIfEmpty(),
                WgPresharedKey = (peerDic.TryGetValue("PresharedKey", out var presharedKey) ? presharedKey : string.Empty).NullIfEmpty(),
                WgInterfaceAddress = wgInterfaceAddress,
                WgReserved = (peerDic.TryGetValue("Reserved", out var reserved) ? reserved : string.Empty).Replace(", ", ",").NullIfEmpty(),
                WgMtu = wgMtu > 0 ? wgMtu : null,
            };

            var item = new ProfileItem
            {
                Remarks = $"{nameof(EConfigType.WireGuard)} Peer {index + 1}",
                ConfigType = EConfigType.WireGuard,
                Address = peerAddress,
                Port = peerPort,
                Password = privateKey,
            };
            item.SetProtocolExtra(protoExtra);
            resultList.Add(item);

            index += 1;
        }

        return resultList;
    }
}
