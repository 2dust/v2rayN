using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class ShareHandler
    {

        #region GetShareUrl

        /// <summary>
        /// GetShareUrl
        /// </summary>
        /// <param name="config"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static string GetShareUrl(Config config, int index)
        {
            try
            {
                string url = string.Empty;

                VmessItem item = config.vmess[index];
                if (item.configType == (int)EConfigType.Vmess)
                {
                    VmessQRCode vmessQRCode = new VmessQRCode
                    {
                        v = item.configVersion.ToString(),
                        ps = item.remarks.TrimEx(), //备注也许很长 ;
                        add = item.address,
                        port = item.port.ToString(),
                        id = item.id,
                        aid = item.alterId.ToString(),
                        net = item.network,
                        type = item.headerType,
                        host = item.requestHost,
                        path = item.path,
                        tls = item.streamSecurity,
                        sni = item.sni
                    };

                    url = Utils.ToJson(vmessQRCode);
                    url = Utils.Base64Encode(url);
                    url = string.Format("{0}{1}", Global.vmessProtocol, url);

                }
                else if (item.configType == (int)EConfigType.Shadowsocks)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(item.remarks))
                    {
                        remark = "#" + Utils.UrlEncode(item.remarks);
                    }
                    url = string.Format("{0}:{1}@{2}:{3}",
                        item.security,
                        item.id,
                        item.address,
                        item.port);
                    url = Utils.Base64Encode(url);
                    url = string.Format("{0}{1}{2}", Global.ssProtocol, url, remark);
                }
                else if (item.configType == (int)EConfigType.Socks)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(item.remarks))
                    {
                        remark = "#" + Utils.UrlEncode(item.remarks);
                    }
                    url = string.Format("{0}:{1}@{2}:{3}",
                        item.security,
                        item.id,
                        item.address,
                        item.port);
                    url = Utils.Base64Encode(url);
                    url = string.Format("{0}{1}{2}", Global.socksProtocol, url, remark);
                }
                else if (item.configType == (int)EConfigType.Trojan)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(item.remarks))
                    {
                        remark = "#" + Utils.UrlEncode(item.remarks);
                    }
                    string query = string.Empty;
                    if (!Utils.IsNullOrEmpty(item.sni))
                    {
                        query = string.Format("?sni={0}", Utils.UrlEncode(item.sni));
                    }
                    url = string.Format("{0}@{1}:{2}",
                        item.id,
                        item.address,
                        item.port);
                    url = string.Format("{0}{1}{2}{3}", Global.trojanProtocol, url, query, remark);
                }
                else if (item.configType == (int)EConfigType.VLESS)
                {
                    string remark = string.Empty;
                    if (!Utils.IsNullOrEmpty(item.remarks))
                    {
                        remark = "#" + Utils.UrlEncode(item.remarks);
                    }
                    var dicQuery = new Dictionary<string, string>();
                    if (!Utils.IsNullOrEmpty(item.flow))
                    {
                        dicQuery.Add("flow", item.flow);
                    }
                    if (!Utils.IsNullOrEmpty(item.security))
                    {
                        dicQuery.Add("encryption", item.security);
                    }
                    else
                    {
                        dicQuery.Add("encryption", "none");
                    }
                    if (!Utils.IsNullOrEmpty(item.streamSecurity))
                    {
                        dicQuery.Add("security", item.streamSecurity);
                    }
                    else
                    {
                        dicQuery.Add("security", "none");
                    }
                    if (!Utils.IsNullOrEmpty(item.sni))
                    {
                        dicQuery.Add("sni", item.sni);
                    }
                    if (!Utils.IsNullOrEmpty(item.network))
                    {
                        dicQuery.Add("type", item.network);
                    }
                    else
                    {
                        dicQuery.Add("type", "tcp");
                    }

                    switch (item.network)
                    {
                        case "tcp":
                            if (!Utils.IsNullOrEmpty(item.headerType))
                            {
                                dicQuery.Add("headerType", item.headerType);
                            }
                            else
                            {
                                dicQuery.Add("headerType", "none");
                            }
                            if (!Utils.IsNullOrEmpty(item.requestHost))
                            {
                                dicQuery.Add("host", Utils.UrlEncode(item.requestHost));
                            }
                            break;
                        case "kcp":
                            if (!Utils.IsNullOrEmpty(item.headerType))
                            {
                                dicQuery.Add("headerType", item.headerType);
                            }
                            else
                            {
                                dicQuery.Add("headerType", "none");
                            }
                            if (!Utils.IsNullOrEmpty(item.path))
                            {
                                dicQuery.Add("seed", Utils.UrlEncode(item.path));
                            }
                            break;

                        case "ws":
                            if (!Utils.IsNullOrEmpty(item.requestHost))
                            {
                                dicQuery.Add("host", Utils.UrlEncode(item.requestHost));
                            }
                            if (!Utils.IsNullOrEmpty(item.path))
                            {
                                dicQuery.Add("path", Utils.UrlEncode(item.path));
                            }
                            break;

                        case "http":
                        case "h2":
                            dicQuery["type"] = "http";
                            if (!Utils.IsNullOrEmpty(item.requestHost))
                            {
                                dicQuery.Add("host", Utils.UrlEncode(item.requestHost));
                            }
                            if (!Utils.IsNullOrEmpty(item.path))
                            {
                                dicQuery.Add("path", Utils.UrlEncode(item.path));
                            }
                            break;

                        case "quic":
                            if (!Utils.IsNullOrEmpty(item.headerType))
                            {
                                dicQuery.Add("headerType", item.headerType);
                            }
                            else
                            {
                                dicQuery.Add("headerType", "none");
                            }
                            dicQuery.Add("quicSecurity", Utils.UrlEncode(item.requestHost));
                            dicQuery.Add("key", Utils.UrlEncode(item.path));
                            break;
                    }
                    string query = "?" + string.Join("&", dicQuery.Select(x => x.Key + "=" + x.Value).ToArray());

                    url = string.Format("{0}@{1}:{2}",
                    item.id,
                    item.address,
                    item.port);
                    url = string.Format("{0}{1}{2}{3}", Global.vlessProtocol, url, query, remark);
                }
                else
                {
                }
                return url;
            }
            catch
            {
                return "";
            }
        }

        #endregion

        #region  ImportShareUrl 


        /// <summary>
        /// 从剪贴板导入URL
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static VmessItem ImportFromClipboardConfig(string clipboardData, out string msg)
        {
            msg = string.Empty;
            VmessItem vmessItem = new VmessItem();

            try
            {
                //载入配置文件 
                string result = clipboardData.TrimEx();// Utils.GetClipboardData();
                if (Utils.IsNullOrEmpty(result))
                {
                    msg = UIRes.I18N("FailedReadConfiguration");
                    return null;
                }

                if (result.StartsWith(Global.vmessProtocol))
                {
                    int indexSplit = result.IndexOf("?");
                    if (indexSplit > 0)
                    {
                        vmessItem = ResolveStdVmess(result) ?? ResolveVmess4Kitsunebi(result);
                    }
                    else
                    {
                        vmessItem.configType = (int)EConfigType.Vmess;
                        result = result.Substring(Global.vmessProtocol.Length);
                        result = Utils.Base64Decode(result);

                        //转成Json
                        VmessQRCode vmessQRCode = Utils.FromJson<VmessQRCode>(result);
                        if (vmessQRCode == null)
                        {
                            msg = UIRes.I18N("FailedConversionConfiguration");
                            return null;
                        }
                        vmessItem.security = Global.DefaultSecurity;
                        vmessItem.network = Global.DefaultNetwork;
                        vmessItem.headerType = Global.None;


                        vmessItem.configVersion = Utils.ToInt(vmessQRCode.v);
                        vmessItem.remarks = Utils.ToString(vmessQRCode.ps);
                        vmessItem.address = Utils.ToString(vmessQRCode.add);
                        vmessItem.port = Utils.ToInt(vmessQRCode.port);
                        vmessItem.id = Utils.ToString(vmessQRCode.id);
                        vmessItem.alterId = Utils.ToInt(vmessQRCode.aid);

                        if (!Utils.IsNullOrEmpty(vmessQRCode.net))
                        {
                            vmessItem.network = vmessQRCode.net;
                        }
                        if (!Utils.IsNullOrEmpty(vmessQRCode.type))
                        {
                            vmessItem.headerType = vmessQRCode.type;
                        }

                        vmessItem.requestHost = Utils.ToString(vmessQRCode.host);
                        vmessItem.path = Utils.ToString(vmessQRCode.path);
                        vmessItem.streamSecurity = Utils.ToString(vmessQRCode.tls);
                        vmessItem.sni = Utils.ToString(vmessQRCode.sni);
                    }

                    ConfigHandler.UpgradeServerVersion(ref vmessItem);
                }
                else if (result.StartsWith(Global.ssProtocol))
                {
                    msg = UIRes.I18N("ConfigurationFormatIncorrect");

                    vmessItem = ResolveSSLegacy(result);
                    if (vmessItem == null)
                    {
                        vmessItem = ResolveSip002(result);
                    }
                    if (vmessItem == null)
                    {
                        return null;
                    }
                    if (vmessItem.address.Length == 0 || vmessItem.port == 0 || vmessItem.security.Length == 0 || vmessItem.id.Length == 0)
                    {
                        return null;
                    }

                    vmessItem.configType = (int)EConfigType.Shadowsocks;
                }
                else if (result.StartsWith(Global.socksProtocol))
                {
                    msg = UIRes.I18N("ConfigurationFormatIncorrect");

                    vmessItem.configType = (int)EConfigType.Socks;
                    result = result.Substring(Global.socksProtocol.Length);
                    //remark
                    int indexRemark = result.IndexOf("#");
                    if (indexRemark > 0)
                    {
                        try
                        {
                            vmessItem.remarks = Utils.UrlDecode(result.Substring(indexRemark + 1, result.Length - indexRemark - 1));
                        }
                        catch { }
                        result = result.Substring(0, indexRemark);
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
                    vmessItem.address = arr1[1].Substring(0, indexPort);
                    vmessItem.port = Utils.ToInt(arr1[1].Substring(indexPort + 1, arr1[1].Length - (indexPort + 1)));
                    vmessItem.security = arr21[0];
                    vmessItem.id = arr21[1];
                }
                else if (result.StartsWith(Global.trojanProtocol))
                {
                    msg = UIRes.I18N("ConfigurationFormatIncorrect");

                    vmessItem.configType = (int)EConfigType.Trojan;

                    Uri uri = new Uri(result);
                    vmessItem.address = uri.IdnHost;
                    vmessItem.port = uri.Port;
                    vmessItem.id = uri.UserInfo;

                    var qurery = HttpUtility.ParseQueryString(uri.Query);
                    vmessItem.sni = qurery["sni"] ?? "";

                    var remarks = uri.Fragment.Replace("#", "");
                    if (Utils.IsNullOrEmpty(remarks))
                    {
                        vmessItem.remarks = "NONE";
                    }
                    else
                    {
                        vmessItem.remarks = Utils.UrlDecode(remarks);
                    }
                }
                else if (result.StartsWith(Global.vlessProtocol))
                {
                    vmessItem = ResolveStdVLESS(result);

                    ConfigHandler.UpgradeServerVersion(ref vmessItem);
                }
                else
                {
                    msg = UIRes.I18N("NonvmessOrssProtocol");
                    return null;
                }
            }
            catch
            {
                msg = UIRes.I18N("Incorrectconfiguration");
                return null;
            }

            return vmessItem;
        }


        private static VmessItem ResolveVmess4Kitsunebi(string result)
        {
            VmessItem vmessItem = new VmessItem
            {
                configType = (int)EConfigType.Vmess
            };
            result = result.Substring(Global.vmessProtocol.Length);
            int indexSplit = result.IndexOf("?");
            if (indexSplit > 0)
            {
                result = result.Substring(0, indexSplit);
            }
            result = Utils.Base64Decode(result);

            string[] arr1 = result.Split('@');
            if (arr1.Length != 2)
            {
                return null;
            }
            string[] arr21 = arr1[0].Split(':');
            string[] arr22 = arr1[1].Split(':');
            if (arr21.Length != 2 || arr21.Length != 2)
            {
                return null;
            }

            vmessItem.address = arr22[0];
            vmessItem.port = Utils.ToInt(arr22[1]);
            vmessItem.security = arr21[0];
            vmessItem.id = arr21[1];

            vmessItem.network = Global.DefaultNetwork;
            vmessItem.headerType = Global.None;
            vmessItem.remarks = "Alien";
            vmessItem.alterId = 0;

            return vmessItem;
        }

        private static VmessItem ResolveSip002(string result)
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
            VmessItem server = new VmessItem
            {
                remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
                address = parsedUrl.IdnHost,
                port = parsedUrl.Port,
            };

            // parse base64 UserInfo
            string rawUserInfo = parsedUrl.GetComponents(UriComponents.UserInfo, UriFormat.Unescaped);
            string base64 = rawUserInfo.Replace('-', '+').Replace('_', '/');    // Web-safe base64 to normal base64
            string userInfo;
            try
            {
                userInfo = Encoding.UTF8.GetString(Convert.FromBase64String(
                base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=')));
            }
            catch (FormatException)
            {
                return null;
            }
            string[] userInfoParts = userInfo.Split(new char[] { ':' }, 2);
            if (userInfoParts.Length != 2)
            {
                return null;
            }
            server.security = userInfoParts[0];
            server.id = userInfoParts[1];

            NameValueCollection queryParameters = HttpUtility.ParseQueryString(parsedUrl.Query);
            if (queryParameters["plugin"] != null)
            {
                return null;
            }

            return server;
        }

        private static readonly Regex UrlFinder = new Regex(@"ss://(?<base64>[A-Za-z0-9+-/=_]+)(?:#(?<tag>\S+))?", RegexOptions.IgnoreCase);
        private static readonly Regex DetailsParser = new Regex(@"^((?<method>.+?):(?<password>.*)@(?<hostname>.+?):(?<port>\d+?))$", RegexOptions.IgnoreCase);

        private static VmessItem ResolveSSLegacy(string result)
        {
            var match = UrlFinder.Match(result);
            if (!match.Success)
                return null;

            VmessItem server = new VmessItem();
            var base64 = match.Groups["base64"].Value.TrimEnd('/');
            var tag = match.Groups["tag"].Value;
            if (!Utils.IsNullOrEmpty(tag))
            {
                server.remarks = Utils.UrlDecode(tag);
            }
            Match details;
            try
            {
                details = DetailsParser.Match(Encoding.UTF8.GetString(Convert.FromBase64String(
                    base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='))));
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
            server.port = int.Parse(details.Groups["port"].Value);
            return server;
        }


        private static readonly Regex StdVmessUserInfo = new Regex(
            @"^(?<network>[a-z]+)(\+(?<streamSecurity>[a-z]+))?:(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})-(?<alterId>[0-9]+)$");

        private static VmessItem ResolveStdVmess(string result)
        {
            VmessItem i = new VmessItem
            {
                configType = (int)EConfigType.Vmess,
                security = "auto"
            };

            Uri u = new Uri(result);

            i.address = u.IdnHost;
            i.port = u.Port;
            i.remarks = u.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            var q = HttpUtility.ParseQueryString(u.Query);

            var m = StdVmessUserInfo.Match(u.UserInfo);
            if (!m.Success) return null;

            i.id = m.Groups["id"].Value;
            if (!int.TryParse(m.Groups["alterId"].Value, out int aid))
            {
                return null;
            }
            i.alterId = aid;

            if (m.Groups["streamSecurity"].Success)
            {
                i.streamSecurity = m.Groups["streamSecurity"].Value;
            }
            switch (i.streamSecurity)
            {
                case "tls":
                    // TODO tls config
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(i.streamSecurity))
                        return null;
                    break;
            }

            i.network = m.Groups["network"].Value;
            switch (i.network)
            {
                case "tcp":
                    string t1 = q["type"] ?? "none";
                    i.headerType = t1;
                    // TODO http option

                    break;
                case "kcp":
                    i.headerType = q["type"] ?? "none";
                    // TODO kcp seed
                    break;

                case "ws":
                    string p1 = q["path"] ?? "/";
                    string h1 = q["host"] ?? "";
                    i.requestHost = Utils.UrlDecode(h1);
                    i.path = p1;
                    break;

                case "http":
                case "h2":
                    i.network = "h2";
                    string p2 = q["path"] ?? "/";
                    string h2 = q["host"] ?? "";
                    i.requestHost = Utils.UrlDecode(h2);
                    i.path = p2;
                    break;

                case "quic":
                    string s = q["security"] ?? "none";
                    string k = q["key"] ?? "";
                    string t3 = q["type"] ?? "none";
                    i.headerType = t3;
                    i.requestHost = Utils.UrlDecode(s);
                    i.path = k;
                    break;

                default:
                    return null;
            }

            return i;
        }

        private static VmessItem ResolveStdVLESS(string result)
        {
            VmessItem item = new VmessItem
            {
                configType = (int)EConfigType.VLESS,
                security = "none"
            };

            Uri url = new Uri(result);

            item.address = url.IdnHost;
            item.port = url.Port;
            item.remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            item.id = url.UserInfo;

            var query = HttpUtility.ParseQueryString(url.Query);

            item.flow = query["flow"] ?? "";
            item.security = query["encryption"] ?? "none";
            item.streamSecurity = query["security"] ?? "";
            item.sni = query["sni"] ?? "";
            item.network = query["type"] ?? "tcp";
            switch (item.network)
            {
                case "tcp":
                    item.headerType = query["headerType"] ?? "none";
                    item.requestHost = Utils.UrlDecode(query["host"] ?? "");

                    break;
                case "kcp":
                    item.headerType = query["headerType"] ?? "none";
                    item.path = Utils.UrlDecode(query["seed"] ?? "");
                    break;

                case "ws":
                    item.requestHost = Utils.UrlDecode(query["host"] ?? "");
                    item.path = Utils.UrlDecode(query["path"] ?? "/");
                    break;

                case "http":
                case "h2":
                    item.network = "h2";
                    item.requestHost = Utils.UrlDecode(query["host"] ?? "");
                    item.path = Utils.UrlDecode(query["path"] ?? "/");
                    break;

                case "quic":
                    item.headerType = query["headerType"] ?? "none";
                    item.requestHost = query["quicSecurity"] ?? "none";
                    item.path = Utils.UrlDecode(query["key"] ?? "");
                    break;

                default:
                    return null;
            }

            return item;
        }

        #endregion
    }
}
