using System.Collections.Specialized;
using System.Text.RegularExpressions;
using v2rayN.Models;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    internal class ShareHandler
    {
        #region GetShareUrl

        /// <summary>
        /// GetShareUrl
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static string? GetShareUrl(ProfileItem item)
        {
            try
            {
                string? url = string.Empty;

                url = item.configType switch
                {
                    EConfigType.VMess => ShareVmess(item),
                    EConfigType.Shadowsocks => ShareShadowsocks(item),
                    EConfigType.Socks => ShareSocks(item),
                    EConfigType.Trojan => ShareTrojan(item),
                    EConfigType.VLESS => ShareVLESS(item),
                    EConfigType.Hysteria2 => ShareHysteria2(item),
                    EConfigType.Tuic => ShareTuic(item),
                    EConfigType.Wireguard => ShareWireguard(item),
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

        private static string ShareVmess(ProfileItem item)
        {
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

        private static string ShareShadowsocks(ProfileItem item)
        {
            string url = string.Empty;

            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            //url = string.Format("{0}:{1}@{2}:{3}",
            //    item.security,
            //    item.id,
            //    item.address,
            //    item.port);
            //url = Utile.Base64Encode(url);
            //new Sip002
            var pw = Utils.Base64Encode($"{item.security}:{item.id}");
            url = $"{pw}@{GetIpv6(item.address)}:{item.port}";
            url = $"{Global.ProtocolShares[EConfigType.Shadowsocks]}{url}{remark}";
            return url;
        }

        private static string ShareSocks(ProfileItem item)
        {
            string url = string.Empty;
            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            //url = string.Format("{0}:{1}@{2}:{3}",
            //    item.security,
            //    item.id,
            //    item.address,
            //    item.port);
            //url = Utile.Base64Encode(url);
            //new
            var pw = Utils.Base64Encode($"{item.security}:{item.id}");
            url = $"{pw}@{GetIpv6(item.address)}:{item.port}";
            url = $"{Global.ProtocolShares[EConfigType.Socks]}{url}{remark}";
            return url;
        }

        private static string ShareTrojan(ProfileItem item)
        {
            string url = string.Empty;
            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            GetStdTransport(item, null, ref dicQuery);
            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            item.id,
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.Trojan]}{url}{query}{remark}";
            return url;
        }

        private static string ShareVLESS(ProfileItem item)
        {
            string url = string.Empty;
            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (!Utils.IsNullOrEmpty(item.security))
            {
                dicQuery.Add("encryption", item.security);
            }
            else
            {
                dicQuery.Add("encryption", Global.None);
            }
            GetStdTransport(item, Global.None, ref dicQuery);
            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            item.id,
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.VLESS]}{url}{query}{remark}";
            return url;
        }

        private static string ShareHysteria2(ProfileItem item)
        {
            string url = string.Empty;
            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (!Utils.IsNullOrEmpty(item.sni))
            {
                dicQuery.Add("sni", item.sni);
            }
            if (!Utils.IsNullOrEmpty(item.alpn))
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.alpn));
            }
            if (!Utils.IsNullOrEmpty(item.path))
            {
                dicQuery.Add("obfs", "salamander");
                dicQuery.Add("obfs-password", Utils.UrlEncode(item.path));
            }
            dicQuery.Add("insecure", item.allowInsecure.ToLower() == "true" ? "1" : "0");

            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            item.id,
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.Hysteria2]}{url}/{query}{remark}";
            return url;
        }

        private static string ShareTuic(ProfileItem item)
        {
            string url = string.Empty;
            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }
            var dicQuery = new Dictionary<string, string>();
            if (!Utils.IsNullOrEmpty(item.sni))
            {
                dicQuery.Add("sni", item.sni);
            }
            if (!Utils.IsNullOrEmpty(item.alpn))
            {
                dicQuery.Add("alpn", Utils.UrlEncode(item.alpn));
            }
            dicQuery.Add("congestion_control", item.headerType);

            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            $"{item.id}:{item.security}",
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.Tuic]}{url}{query}{remark}";
            return url;
        }

        private static string ShareWireguard(ProfileItem item)
        {
            string url = string.Empty;
            string remark = string.Empty;
            if (!Utils.IsNullOrEmpty(item.remarks))
            {
                remark = "#" + Utils.UrlEncode(item.remarks);
            }

            var dicQuery = new Dictionary<string, string>();
            if (!Utils.IsNullOrEmpty(item.publicKey))
            {
                dicQuery.Add("publickey", Utils.UrlEncode(item.publicKey));
            }
            if (!Utils.IsNullOrEmpty(item.path))
            {
                dicQuery.Add("reserved", Utils.UrlEncode(item.path));
            }
            if (!Utils.IsNullOrEmpty(item.requestHost))
            {
                dicQuery.Add("address", Utils.UrlEncode(item.requestHost));
            }
            if (!Utils.IsNullOrEmpty(item.shortId))
            {
                dicQuery.Add("mtu", Utils.UrlEncode(item.shortId));
            }
            string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

            url = string.Format("{0}@{1}:{2}",
            Utils.UrlEncode(item.id),
            GetIpv6(item.address),
            item.port);
            url = $"{Global.ProtocolShares[EConfigType.Wireguard]}{url}/{query}{remark}";
            return url;
        }

        private static string GetIpv6(string address)
        {
            return Utils.IsIpv6(address) ? $"[{address}]" : address;
        }

        private static int GetStdTransport(ProfileItem item, string? securityDef, ref Dictionary<string, string> dicQuery)
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

        #endregion GetShareUrl

        #region ImportShareUrl

        /// <summary>
        /// 从剪贴板导入URL
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static ProfileItem? ImportFromClipboardConfig(string clipboardData, out string msg)
        {
            msg = ResUI.ConfigurationFormatIncorrect;
            ProfileItem? profileItem;

            try
            {
                string result = clipboardData.TrimEx();
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = ResUI.FailedReadConfiguration;
                    return null;
                }

                if (result.StartsWith(Global.ProtocolShares[EConfigType.VMess]))
                {
                    int indexSplit = result.IndexOf("?");
                    if (indexSplit > 0)
                    {
                        profileItem = ResolveStdVmess(result) ?? ResolveVmess4Kitsunebi(result);
                    }
                    else
                    {
                        profileItem = ResolveVmess(result, out msg);
                    }
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.Shadowsocks]))
                {
                    profileItem = ResolveSSLegacy(result) ?? ResolveSip002(result);
                    if (profileItem == null)
                    {
                        return null;
                    }
                    if (profileItem.address.Length == 0 || profileItem.port == 0 || profileItem.security.Length == 0 || profileItem.id.Length == 0)
                    {
                        return null;
                    }

                    profileItem.configType = EConfigType.Shadowsocks;
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.Socks]))
                {
                    profileItem = ResolveSocksNew(result) ?? ResolveSocks(result);
                    if (profileItem == null)
                    {
                        return null;
                    }
                    if (profileItem.address.Length == 0 || profileItem.port == 0)
                    {
                        return null;
                    }

                    profileItem.configType = EConfigType.Socks;
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.Trojan]))
                {
                    profileItem = ResolveTrojan(result);
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.VLESS]))
                {
                    profileItem = ResolveStdVLESS(result);
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.Hysteria2]) || result.StartsWith(Global.Hysteria2ProtocolShare))
                {
                    profileItem = ResolveHysteria2(result);
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.Tuic]))
                {
                    profileItem = ResolveTuic(result);
                }
                else if (result.StartsWith(Global.ProtocolShares[EConfigType.Wireguard]))
                {
                    profileItem = ResolveWireguard(result);
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

            return profileItem;
        }

        private static ProfileItem? ResolveVmess(string result, out string msg)
        {
            msg = string.Empty;
            var profileItem = new ProfileItem
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

            profileItem.network = Global.DefaultNetwork;
            profileItem.headerType = Global.None;

            profileItem.configVersion = Utils.ToInt(vmessQRCode.v);
            profileItem.remarks = Utils.ToString(vmessQRCode.ps);
            profileItem.address = Utils.ToString(vmessQRCode.add);
            profileItem.port = Utils.ToInt(vmessQRCode.port);
            profileItem.id = Utils.ToString(vmessQRCode.id);
            profileItem.alterId = Utils.ToInt(vmessQRCode.aid);
            profileItem.security = Utils.ToString(vmessQRCode.scy);

            profileItem.security = !Utils.IsNullOrEmpty(vmessQRCode.scy) ? vmessQRCode.scy : Global.DefaultSecurity;
            if (!Utils.IsNullOrEmpty(vmessQRCode.net))
            {
                profileItem.network = vmessQRCode.net;
            }
            if (!Utils.IsNullOrEmpty(vmessQRCode.type))
            {
                profileItem.headerType = vmessQRCode.type;
            }

            profileItem.requestHost = Utils.ToString(vmessQRCode.host);
            profileItem.path = Utils.ToString(vmessQRCode.path);
            profileItem.streamSecurity = Utils.ToString(vmessQRCode.tls);
            profileItem.sni = Utils.ToString(vmessQRCode.sni);
            profileItem.alpn = Utils.ToString(vmessQRCode.alpn);
            profileItem.fingerprint = Utils.ToString(vmessQRCode.fp);

            return profileItem;
        }

        private static ProfileItem? ResolveVmess4Kitsunebi(string result)
        {
            ProfileItem profileItem = new()
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

            profileItem.address = arr22[0];
            profileItem.port = Utils.ToInt(arr22[1]);
            profileItem.security = arr21[0];
            profileItem.id = arr21[1];

            profileItem.network = Global.DefaultNetwork;
            profileItem.headerType = Global.None;
            profileItem.remarks = "Alien";

            return profileItem;
        }

        private static ProfileItem? ResolveStdVmess(string result)
        {
            ProfileItem i = new()
            {
                configType = EConfigType.VMess,
                security = "auto"
            };

            Uri u = new(result);

            i.address = u.IdnHost;
            i.port = u.Port;
            i.remarks = u.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            var query = Utils.ParseQueryString(u.Query);

            var m = StdVmessUserInfo.Match(u.UserInfo);
            if (!m.Success) return null;

            i.id = m.Groups["id"].Value;

            if (m.Groups["streamSecurity"].Success)
            {
                i.streamSecurity = m.Groups["streamSecurity"].Value;
            }
            switch (i.streamSecurity)
            {
                case Global.StreamSecurity:
                    break;

                default:
                    if (!Utils.IsNullOrEmpty(i.streamSecurity))
                        return null;
                    break;
            }

            i.network = m.Groups["network"].Value;
            switch (i.network)
            {
                case nameof(ETransport.tcp):
                    string t1 = query["type"] ?? Global.None;
                    i.headerType = t1;
                    break;

                case nameof(ETransport.kcp):
                    i.headerType = query["type"] ?? Global.None;
                    break;

                case nameof(ETransport.ws):
                case nameof(ETransport.httpupgrade):
                    string p1 = query["path"] ?? "/";
                    string h1 = query["host"] ?? "";
                    i.requestHost = Utils.UrlDecode(h1);
                    i.path = p1;
                    break;

                case nameof(ETransport.http):
                case nameof(ETransport.h2):
                    i.network = nameof(ETransport.h2);
                    string p2 = query["path"] ?? "/";
                    string h2 = query["host"] ?? "";
                    i.requestHost = Utils.UrlDecode(h2);
                    i.path = p2;
                    break;

                case nameof(ETransport.quic):
                    string s = query["security"] ?? Global.None;
                    string k = query["key"] ?? "";
                    string t3 = query["type"] ?? Global.None;
                    i.headerType = t3;
                    i.requestHost = Utils.UrlDecode(s);
                    i.path = k;
                    break;

                default:
                    return null;
            }

            return i;
        }

        private static ProfileItem? ResolveSip002(string result)
        {
            Uri parsedUrl;
            try
            {
                parsedUrl = new Uri(result);
            }
            catch (UriFormatException)
            {
                return null;
            }
            ProfileItem server = new()
            {
                remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
                address = parsedUrl.IdnHost,
                port = parsedUrl.Port,
            };
            string rawUserInfo = parsedUrl.GetComponents(UriComponents.UserInfo, UriFormat.UriEscaped);
            //2022-blake3
            if (rawUserInfo.Contains(':'))
            {
                string[] userInfoParts = rawUserInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    return null;
                }
                server.security = userInfoParts[0];
                server.id = Utils.UrlDecode(userInfoParts[1]);
            }
            else
            {
                // parse base64 UserInfo
                string userInfo = Utils.Base64Decode(rawUserInfo);
                string[] userInfoParts = userInfo.Split(new[] { ':' }, 2);
                if (userInfoParts.Length != 2)
                {
                    return null;
                }
                server.security = userInfoParts[0];
                server.id = userInfoParts[1];
            }

            var queryParameters = Utils.ParseQueryString(parsedUrl.Query);
            if (queryParameters["plugin"] != null)
            {
                //obfs-host exists
                var obfsHost = queryParameters["plugin"]?.Split(';').FirstOrDefault(t => t.Contains("obfs-host"));
                if (queryParameters["plugin"].Contains("obfs=http") && !Utils.IsNullOrEmpty(obfsHost))
                {
                    obfsHost = obfsHost?.Replace("obfs-host=", "");
                    server.network = Global.DefaultNetwork;
                    server.headerType = Global.TcpHeaderHttp;
                    server.requestHost = obfsHost ?? "";
                }
                else
                {
                    return null;
                }
            }

            return server;
        }

        private static readonly Regex UrlFinder = new(@"ss://(?<base64>[A-Za-z0-9+-/=_]+)(?:#(?<tag>\S+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DetailsParser = new(@"^((?<method>.+?):(?<password>.*)@(?<hostname>.+?):(?<port>\d+?))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static ProfileItem? ResolveSSLegacy(string result)
        {
            var match = UrlFinder.Match(result);
            if (!match.Success)
                return null;

            ProfileItem server = new();
            var base64 = match.Groups["base64"].Value.TrimEnd('/');
            var tag = match.Groups["tag"].Value;
            if (!Utils.IsNullOrEmpty(tag))
            {
                server.remarks = Utils.UrlDecode(tag);
            }
            Match details;
            try
            {
                details = DetailsParser.Match(Utils.Base64Decode(base64));
            }
            catch (FormatException)
            {
                return null;
            }
            if (!details.Success)
                return null;
            server.security = details.Groups["method"].Value;
            server.id = details.Groups["password"].Value;
            server.address = details.Groups["hostname"].Value;
            server.port = Utils.ToInt(details.Groups["port"].Value);
            return server;
        }

        private static readonly Regex StdVmessUserInfo = new(
            @"^(?<network>[a-z]+)(\+(?<streamSecurity>[a-z]+))?:(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$", RegexOptions.Compiled);

        private static ProfileItem? ResolveSocks(string result)
        {
            ProfileItem profileItem = new()
            {
                configType = EConfigType.Socks
            };
            result = result[Global.ProtocolShares[EConfigType.Socks].Length..];
            //remark
            int indexRemark = result.IndexOf("#");
            if (indexRemark > 0)
            {
                try
                {
                    profileItem.remarks = Utils.UrlDecode(result.Substring(indexRemark + 1, result.Length - indexRemark - 1));
                }
                catch { }
                result = result[..indexRemark];
            }
            //part decode
            int indexS = result.IndexOf("@");
            if (indexS > 0)
            {
            }
            else
            {
                result = Utils.Base64Decode(result);
            }

            string[] arr1 = result.Split('@');
            if (arr1.Length != 2)
            {
                return null;
            }
            string[] arr21 = arr1[0].Split(':');
            //string[] arr22 = arr1[1].Split(':');
            int indexPort = arr1[1].LastIndexOf(":");
            if (arr21.Length != 2 || indexPort < 0)
            {
                return null;
            }
            profileItem.address = arr1[1][..indexPort];
            profileItem.port = Utils.ToInt(arr1[1][(indexPort + 1)..]);
            profileItem.security = arr21[0];
            profileItem.id = arr21[1];

            return profileItem;
        }

        private static ProfileItem? ResolveSocksNew(string result)
        {
            Uri parsedUrl;
            try
            {
                parsedUrl = new Uri(result);
            }
            catch (UriFormatException)
            {
                return null;
            }
            ProfileItem server = new()
            {
                remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
                address = parsedUrl.IdnHost,
                port = parsedUrl.Port,
            };

            // parse base64 UserInfo
            string rawUserInfo = parsedUrl.GetComponents(UriComponents.UserInfo, UriFormat.Unescaped);
            string userInfo = Utils.Base64Decode(rawUserInfo);
            string[] userInfoParts = userInfo.Split(new[] { ':' }, 2);
            if (userInfoParts.Length == 2)
            {
                server.security = userInfoParts[0];
                server.id = userInfoParts[1];
            }

            return server;
        }

        private static ProfileItem ResolveTrojan(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.Trojan
            };

            Uri url = new(result);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);

            return item;
        }

        private static ProfileItem ResolveStdVLESS(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.VLESS,
                security = Global.None
            };

            Uri url = new(result);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            item.security = query["encryption"] ?? Global.None;
            item.streamSecurity = query["security"] ?? "";
            ResolveStdTransport(query, ref item);

            return item;
        }

        private static ProfileItem ResolveHysteria2(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.Hysteria2
            };

            Uri url = new(result);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);
            item.path = Utils.UrlDecode(query["obfs-password"] ?? "");
            item.allowInsecure = (query["insecure"] ?? "") == "1" ? "true" : "false";

            return item;
        }

        private static ProfileItem ResolveTuic(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.Tuic
            };

            Uri url = new(result);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            var userInfoParts = url.UserInfo.Split(new[] { ':' }, 2);
            if (userInfoParts.Length == 2)
            {
                item.id = userInfoParts[0];
                item.security = userInfoParts[1];
            }

            var query = Utils.ParseQueryString(url.Query);
            ResolveStdTransport(query, ref item);
            item.headerType = query["congestion_control"] ?? "";

            return item;
        }

        private static ProfileItem ResolveWireguard(string result)
        {
            ProfileItem item = new()
            {
                configType = EConfigType.Wireguard
            };

            Uri url = new(result);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = Utils.UrlDecode(url.UserInfo);

            var query = Utils.ParseQueryString(url.Query);

            item.publicKey = Utils.UrlDecode(query["publickey"] ?? "");
            item.path = Utils.UrlDecode(query["reserved"] ?? "");
            item.requestHost = Utils.UrlDecode(query["address"] ?? "");
            item.shortId = Utils.UrlDecode(query["mtu"] ?? "");

            return item;
        }

        private static int ResolveStdTransport(NameValueCollection query, ref ProfileItem item)
        {
            item.flow = query["flow"] ?? "";
            item.streamSecurity = query["security"] ?? "";
            item.sni = query["sni"] ?? "";
            item.alpn = Utils.UrlDecode(query["alpn"] ?? "");
            item.fingerprint = Utils.UrlDecode(query["fp"] ?? "");
            item.publicKey = Utils.UrlDecode(query["pbk"] ?? "");
            item.shortId = Utils.UrlDecode(query["sid"] ?? "");
            item.spiderX = Utils.UrlDecode(query["spx"] ?? "");

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

        #endregion ImportShareUrl
    }
}