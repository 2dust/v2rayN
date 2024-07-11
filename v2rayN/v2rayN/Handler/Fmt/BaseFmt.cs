using System.Collections.Specialized;
using System.IO;
using v2rayN.Enums;
using v2rayN.Models;

namespace v2rayN.Handler.Fmt
{
    internal class BaseFmt
    {
        protected static string GetIpv6(string address)
        {
            if (Utils.IsIpv6(address))
            {
                // 检查地址是否已经被方括号包围，如果没有，则添加方括号
                return address.StartsWith('[') && address.EndsWith(']') ? address : $"[{address}]";
            }
            return address;  // 如果不是IPv6地址，直接返回原地址
        }

        protected static int GetStdTransport(ProfileItem item, string? securityDef, ref Dictionary<string, string> dicQuery)
        {
            if (!Utils.IsNullOrEmpty(item.flow))
            {
                dicQuery.Add("flow", item.flow);
            }

            if (!Utils.IsNullOrEmpty(item.streamSecurity))
            {
                dicQuery.Add("security", item.streamSecurity);
            }
            else
            {
                if (securityDef != null)
                {
                    dicQuery.Add("security", securityDef);
                }
            }
            if (!Utils.IsNullOrEmpty(item.sni))
            {
                dicQuery.Add("sni", item.sni);
            }
            if (!Utils.IsNullOrEmpty(item.alpn))
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.alpn));
            }
            if (!Utils.IsNullOrEmpty(item.fingerprint))
            {
                dicQuery.Add("fp", Utils.UrlEncode(item.fingerprint));
            }
            if (!Utils.IsNullOrEmpty(item.publicKey))
            {
                dicQuery.Add("pbk", Utils.UrlEncode(item.publicKey));
            }
            if (!Utils.IsNullOrEmpty(item.shortId))
            {
                dicQuery.Add("sid", Utils.UrlEncode(item.shortId));
            }
            if (!Utils.IsNullOrEmpty(item.spiderX))
            {
                dicQuery.Add("spx", Utils.UrlEncode(item.spiderX));
            }
            if (item.allowInsecure.Equals("true"))
            {
                dicQuery.Add("allowInsecure", "1");
            }

            dicQuery.Add("type", !Utils.IsNullOrEmpty(item.network) ? item.network : nameof(ETransport.tcp));

            switch (item.network)
            {
                case nameof(ETransport.tcp):
                    dicQuery.Add("headerType", !Utils.IsNullOrEmpty(item.headerType) ? item.headerType : Global.None);
                    if (!Utils.IsNullOrEmpty(item.requestHost))
                    {
                        dicQuery.Add("host", Utils.UrlEncode(item.requestHost));
                    }
                    break;

                case nameof(ETransport.kcp):
                    dicQuery.Add("headerType", !Utils.IsNullOrEmpty(item.headerType) ? item.headerType : Global.None);
                    if (!Utils.IsNullOrEmpty(item.path))
                    {
                        dicQuery.Add("seed", Utils.UrlEncode(item.path));
                    }
                    break;

                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                case nameof(ETransport.splithttp):
                    if (!Utils.IsNullOrEmpty(item.requestHost))
                    {
                        dicQuery.Add("host", Utils.UrlEncode(item.requestHost));
                    }
                    if (!Utils.IsNullOrEmpty(item.path))
                    {
                        dicQuery.Add("path", Utils.UrlEncode(item.path));
                    }
                    break;

                case nameof(ETransport.http):
                case nameof(ETransport.h2):
                    dicQuery["type"] = nameof(ETransport.http);
                    if (!Utils.IsNullOrEmpty(item.requestHost))
                    {
                        dicQuery.Add("host", Utils.UrlEncode(item.requestHost));
                    }
                    if (!Utils.IsNullOrEmpty(item.path))
                    {
                        dicQuery.Add("path", Utils.UrlEncode(item.path));
                    }
                    break;

                case nameof(ETransport.quic):
                    dicQuery.Add("headerType", !Utils.IsNullOrEmpty(item.headerType) ? item.headerType : Global.None);
                    dicQuery.Add("quicSecurity", Utils.UrlEncode(item.requestHost));
                    dicQuery.Add("key", Utils.UrlEncode(item.path));
                    break;

                case nameof(ETransport.grpc):
                    if (!Utils.IsNullOrEmpty(item.path))
                    {
                        dicQuery.Add("authority", Utils.UrlEncode(item.requestHost));
                        dicQuery.Add("serviceName", Utils.UrlEncode(item.path));
                        if (item.headerType is Global.GrpcGunMode or Global.GrpcMultiMode)
                        {
                            dicQuery.Add("mode", Utils.UrlEncode(item.headerType));
                        }
                    }
                    break;
            }
            return 0;
        }

        protected static int ResolveStdTransport(NameValueCollection query, ref ProfileItem item)
        {
            item.flow = query["flow"] ?? "";
            item.streamSecurity = query["security"] ?? "";
            item.sni = query["sni"] ?? "";
            item.alpn = Utils.UrlDecode(query["alpn"] ?? "");
            item.fingerprint = Utils.UrlDecode(query["fp"] ?? "");
            item.publicKey = Utils.UrlDecode(query["pbk"] ?? "");
            item.shortId = Utils.UrlDecode(query["sid"] ?? "");
            item.spiderX = Utils.UrlDecode(query["spx"] ?? "");
            item.allowInsecure = (query["allowInsecure"] ?? "") == "1" ? "true" : "";

            item.network = query["type"] ?? nameof(ETransport.tcp);
            switch (item.network)
            {
                case nameof(ETransport.tcp):
                    item.headerType = query["headerType"] ?? Global.None;
                    item.requestHost = Utils.UrlDecode(query["host"] ?? "");

                    break;

                case nameof(ETransport.kcp):
                    item.headerType = query["headerType"] ?? Global.None;
                    item.path = Utils.UrlDecode(query["seed"] ?? "");
                    break;

                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                case nameof(ETransport.splithttp):
                    item.requestHost = Utils.UrlDecode(query["host"] ?? "");
                    item.path = Utils.UrlDecode(query["path"] ?? "/");
                    break;

                case nameof(ETransport.http):
                case nameof(ETransport.h2):
                    item.network = nameof(ETransport.h2);
                    item.requestHost = Utils.UrlDecode(query["host"] ?? "");
                    item.path = Utils.UrlDecode(query["path"] ?? "/");
                    break;

                case nameof(ETransport.quic):
                    item.headerType = query["headerType"] ?? Global.None;
                    item.requestHost = query["quicSecurity"] ?? Global.None;
                    item.path = Utils.UrlDecode(query["key"] ?? "");
                    break;

                case nameof(ETransport.grpc):
                    item.requestHost = Utils.UrlDecode(query["authority"] ?? "");
                    item.path = Utils.UrlDecode(query["serviceName"] ?? "");
                    item.headerType = Utils.UrlDecode(query["mode"] ?? Global.GrpcGunMode);
                    break;

                default:
                    break;
            }
            return 0;
        }

        protected static bool Contains(string str, params string[] s)
        {
            foreach (var item in s)
            {
                if (str.Contains(item, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        protected static string WriteAllText(string strData, string ext = "json")
        {
            var fileName = Utils.GetTempPath($"{Utils.GetGUID(false)}.{ext}");
            File.WriteAllText(fileName, strData);
            return fileName;
        }
    }
}