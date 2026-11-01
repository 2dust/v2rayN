namespace ServiceLib.Services.CoreConfig;

/// <summary>
/// 把 v2rayN 的节点(ProfileItem)转换成 mihomo(Clash.Meta) 的完整配置内容。
/// 用于让 mihomo 核心直接承载普通节点（含 ShadowsocksR）。
/// </summary>
public class CoreConfigClashProfileService(CoreConfigContext context)
{
    private static readonly string _tag = "CoreConfigClashProfileService";
    private readonly Config _config = context.AppConfig;
    private readonly ProfileItem _node = context.Node;

    public RetResult GenerateClientConfigContent()
    {
        var ret = new RetResult();
        try
        {
            if (_node == null || !_node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            var proxy = BuildProxy(_node);
            var proxyName = proxy["name"]?.ToString() ?? _node.Address;

            var yaml = new Dictionary<string, object>
            {
                ["mixed-port"] = AppManager.Instance.GetLocalPort(EInboundProtocol.socks),
                ["allow-lan"] = _config.Inbound.Count > 0 && _config.Inbound.First().AllowLANConn,
                ["mode"] = "rule",
                ["log-level"] = GetLogLevel(_config.CoreBasicItem.Loglevel),
                ["ipv6"] = _config.ClashUIItem.EnableIPv6,
                ["external-controller"] = $"{Global.Loopback}:{AppManager.Instance.StatePort2}",
                ["proxies"] = new List<object> { proxy },
                ["proxy-groups"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = "PROXY",
                        ["type"] = "select",
                        ["proxies"] = new List<object> { proxyName },
                    },
                },
                ["rules"] = new List<object> { "MATCH,PROXY" },
                ["sniffer"] = BuildSniffer(),
            };

            if (_config.TunModeItem.EnableTun)
            {
                yaml["tun"] = new Dictionary<string, object>
                {
                    ["enable"] = true,
                    ["stack"] = _config.TunModeItem.Stack,
                    ["auto-route"] = _config.TunModeItem.AutoRoute,
                    ["strict-route"] = _config.TunModeItem.StrictRoute,
                    ["dns-hijack"] = new List<object> { "any:53" },
                };
            }

            ret.Data = YamlUtils.ToYaml(yaml);
            ret.Success = true;
            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, _node.GetSummary());
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    /// <summary>
    /// 批量测速用的配置：每个节点分配一个本地 mixed 监听端口，通过 listeners.proxy 固定走该节点。
    /// </summary>
    public RetResult GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds)
    {
        var ret = new RetResult();
        try
        {
            ret.Msg = ResUI.InitialConfiguration;

            var proxies = new List<object>();
            var listeners = new List<object>();
            var (lstIpEndPoints, lstTcpConns) = Utils.GetActiveNetworkInfo();
            var initPort = AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest);

            foreach (var it in selecteds)
            {
                it.AllowTest = false;
                if (!Global.MihomoSupportConfigType.Contains(it.ConfigType))
                {
                    continue;
                }

                var indexId = it.IndexId ?? string.Empty;
                var actIndexId = context.ServerTestItemMap.GetValueOrDefault(indexId, indexId);
                var item = context.AllProxiesMap.GetValueOrDefault(actIndexId) ?? it.Profile;
                if (item is null || item.ConfigType is EConfigType.Custom || !item.IsValid())
                {
                    Logging.SaveLog($"{_tag} speedtest skip index={indexId} hasMap={context.AllProxiesMap.ContainsKey(actIndexId)} hasProfile={it.Profile is not null}");
                    continue;
                }

                var port = 0;
                for (var k = initPort; k < Global.MaxPort; k++)
                {
                    if (lstIpEndPoints?.FindIndex(x => x.Port == k) >= 0)
                    {
                        continue;
                    }
                    if (lstTcpConns?.FindIndex(x => x.LocalEndPoint.Port == k) >= 0)
                    {
                        continue;
                    }
                    port = k;
                    initPort = port + 1;
                    break;
                }
                if (port <= 0)
                {
                    continue;
                }

                it.Port = port;
                it.AllowTest = true;

                var name = $"st{port}";
                proxies.Add(BuildProxy(item, name));
                listeners.Add(new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["type"] = "mixed",
                    ["listen"] = Global.Loopback,
                    ["port"] = port,
                    ["proxy"] = name,
                });
            }

            if (proxies.Count == 0)
            {
                Logging.SaveLog($"{_tag} speedtest no proxy generated, selected={selecteds.Count}");
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            Logging.SaveLog($"{_tag} speedtest config proxies={proxies.Count} listeners={listeners.Count}");

            ret.Data = YamlUtils.ToYaml(new Dictionary<string, object>
            {
                ["allow-lan"] = false,
                ["mode"] = "rule",
                ["log-level"] = "silent",
                ["proxies"] = proxies,
                ["listeners"] = listeners,
                ["rules"] = new List<object> { "MATCH,DIRECT" },
                ["sniffer"] = BuildSniffer(),
            });
            ret.Success = true;
            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    /// <summary>
    /// 单节点测速用的配置：只包含当前节点，并监听指定端口。
    /// </summary>
    public RetResult GenerateClientSpeedtestConfig(int port)
    {
        var ret = new RetResult();
        try
        {
            if (_node == null || !_node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            var name = $"st{port}";
            ret.Data = YamlUtils.ToYaml(new Dictionary<string, object>
            {
                ["allow-lan"] = false,
                ["mode"] = "rule",
                ["log-level"] = "silent",
                ["proxies"] = new List<object> { BuildProxy(_node, name) },
                ["listeners"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = name,
                        ["type"] = "mixed",
                        ["listen"] = Global.Loopback,
                        ["port"] = port,
                        ["proxy"] = name,
                    },
                },
                ["rules"] = new List<object> { "MATCH,DIRECT" },
                ["sniffer"] = BuildSniffer(),
            });
            ret.Success = true;
            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, _node.GetSummary());
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    /// <summary>
    /// 开启域名嗅探：.NET/SOCKS5 客户端会先做本地 DNS 解析（国内常被污染），
    /// 把连接目标变成错误的 IP。sniffer 可以从 TLS SNI / HTTP Host 还原真实域名，
    /// 交给代理侧解析，避免测速和访问被污染结果拖死。
    /// </summary>
    private Dictionary<string, object> BuildSniffer()
    {
        var forceDomain = new List<string>
        {
            "+.google.com",
            "+.gstatic.com",
            "+.googleapis.com",
            "+.youtube.com",
            "+.cloudflare.com",
            "+.cloudflare-dns.com",
            "+.github.com",
            "+.githubusercontent.com",
        };

        var speedTest = _config.SpeedTestItem;
        foreach (var url in new[] { speedTest.SpeedPingTestUrl, speedTest.SpeedTestUrl, speedTest.IPAPIUrl })
        {
            if (url.IsNullOrEmpty())
            {
                continue;
            }
            try
            {
                var host = new Uri(url).Host;
                if (host.IsNotEmpty() && !forceDomain.Contains(host))
                {
                    forceDomain.Add(host);
                    forceDomain.Add($"+.{host}");
                }
            }
            catch
            {
                // ignore invalid url
            }
        }

        return new Dictionary<string, object>
        {
            ["enable"] = true,
            ["parse-pure-ip"] = true,
            ["override-destination"] = true,
            ["force-domain"] = forceDomain,
            ["sniff"] = new Dictionary<string, object>
            {
                ["TLS"] = new Dictionary<string, object> { ["ports"] = new List<object> { 443, 8443 } },
                ["HTTP"] = new Dictionary<string, object> { ["ports"] = new List<object> { 80, "8080-8880" } },
            },
        };
    }

    private Dictionary<string, object> BuildProxy(ProfileItem node, string? nameOverride = null)
    {
        var extra = node.GetProtocolExtra();
        var transport = node.GetTransportExtra();
        var name = nameOverride ?? (node.Remarks.IsNotEmpty() ? node.Remarks : $"{node.Address}:{node.Port}");

        var proxy = new Dictionary<string, object>
        {
            ["name"] = name,
            ["server"] = node.Address,
            ["port"] = node.Port,
        };

        switch (node.ConfigType)
        {
            case EConfigType.ShadowsocksR:
                proxy["type"] = "ssr";
                proxy["cipher"] = extra.SsMethod ?? string.Empty;
                proxy["password"] = node.Password;
                proxy["obfs"] = extra.SsrObfs ?? "plain";
                proxy["protocol"] = extra.SsrProtocol ?? "origin";
                if (extra.SsrObfsParam.IsNotEmpty())
                {
                    proxy["obfs-param"] = extra.SsrObfsParam;
                }
                if (extra.SsrProtocolParam.IsNotEmpty())
                {
                    proxy["protocol-param"] = extra.SsrProtocolParam;
                }
                proxy["udp"] = true;
                break;

            case EConfigType.Shadowsocks:
                proxy["type"] = "ss";
                proxy["cipher"] = extra.SsMethod ?? string.Empty;
                proxy["password"] = node.Password;
                proxy["udp"] = true;
                break;

            case EConfigType.VMess:
                proxy["type"] = "vmess";
                proxy["uuid"] = node.Password;
                proxy["alterId"] = extra.AlterId.ToInt();
                proxy["cipher"] = extra.VmessSecurity.IsNotEmpty() ? extra.VmessSecurity : "auto";
                proxy["udp"] = true;
                break;

            case EConfigType.VLESS:
                proxy["type"] = "vless";
                proxy["uuid"] = node.Password;
                proxy["udp"] = true;
                if (extra.Flow.IsNotEmpty())
                {
                    proxy["flow"] = extra.Flow;
                }
                break;

            case EConfigType.Trojan:
                proxy["type"] = "trojan";
                proxy["password"] = node.Password;
                proxy["udp"] = true;
                break;

            case EConfigType.HTTP:
                proxy["type"] = "http";
                proxy["username"] = node.Username;
                proxy["password"] = node.Password;
                break;

            case EConfigType.SOCKS:
                proxy["type"] = "socks5";
                proxy["username"] = node.Username;
                proxy["password"] = node.Password;
                proxy["udp"] = true;
                break;

            default:
                throw new NotSupportedException($"mihomo core not support config type [{node.ConfigType}]");
        }

        // tls
        if (node.StreamSecurity == Global.StreamSecurity)
        {
            proxy["tls"] = true;
            var sni = node.Sni.IsNotEmpty() ? node.Sni : transport.Host;
            if (sni.IsNotEmpty())
            {
                proxy["servername"] = sni;
            }
            if (node.GetAllowInsecure())
            {
                proxy["skip-cert-verify"] = true;
            }
            if (node.Alpn.IsNotEmpty())
            {
                proxy["alpn"] = node.GetAlpn();
            }
            if (node.Fingerprint.IsNotEmpty())
            {
                proxy["client-fingerprint"] = node.Fingerprint;
            }
        }

        // transport
        var network = node.GetNetwork();
        switch (network)
        {
            case nameof(ETransport.ws):
                proxy["network"] = "ws";
                var wsOpts = new Dictionary<string, object>
                {
                    ["path"] = transport.Path.IsNotEmpty() ? transport.Path : "/",
                };
                if (transport.Host.IsNotEmpty())
                {
                    wsOpts["headers"] = new Dictionary<string, object> { ["Host"] = transport.Host };
                }
                proxy["ws-opts"] = wsOpts;
                break;

            case nameof(ETransport.grpc):
                proxy["network"] = "grpc";
                proxy["grpc-opts"] = new Dictionary<string, object>
                {
                    ["grpc-service-name"] = transport.GrpcServiceName ?? string.Empty,
                };
                break;

            case nameof(ETransport.httpupgrade):
                proxy["network"] = "ws";
                var huOpts = new Dictionary<string, object>
                {
                    ["path"] = transport.Path.IsNotEmpty() ? transport.Path : "/",
                    ["v2ray-http-upgrade"] = true,
                };
                if (transport.Host.IsNotEmpty())
                {
                    huOpts["headers"] = new Dictionary<string, object> { ["Host"] = transport.Host };
                }
                proxy["ws-opts"] = huOpts;
                break;
        }

        return proxy;
    }

    private string GetLogLevel(string level)
    {
        return level == "none" ? "silent" : level;
    }
}
