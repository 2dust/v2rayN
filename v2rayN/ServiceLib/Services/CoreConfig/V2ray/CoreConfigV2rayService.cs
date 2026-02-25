namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService(CoreConfigContext context)
{
    private static readonly string _tag = "CoreConfigV2rayService";
    private readonly Config _config = context.AppConfig;
    private readonly ProfileItem _node = context.Node;

    private V2rayConfig _coreConfig = new();

    #region public gen function

    public RetResult GenerateClientConfigContent()
    {
        var ret = new RetResult();
        try
        {
            if (context.IsTunEnabled && context.TunProtectSsPort > 0 && context.ProxyRelaySsPort > 0)
            {
                return GenerateClientProxyRelayConfig();
            }
            if (_node == null
                || !_node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (_node.GetNetwork() is nameof(ETransport.quic))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {_node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            var result = EmbedUtils.GetEmbedText(Global.V2raySampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (_coreConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            GenLog();

            GenInbounds();

            GenOutbounds();

            GenRouting();

            GenDns();

            GenStatistic();

            var finalRule = BuildFinalRule();
            if (!string.IsNullOrEmpty(finalRule?.balancerTag))
            {
                _coreConfig.routing.rules.Add(finalRule);
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = ApplyFullConfigTemplate();
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public RetResult GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds)
    {
        var ret = new RetResult();
        try
        {
            ret.Msg = ResUI.InitialConfiguration;

            var result = EmbedUtils.GetEmbedText(Global.V2raySampleClient);
            var txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (_coreConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            var (lstIpEndPoints, lstTcpConns) = Utils.GetActiveNetworkInfo();

            GenLog();
            _coreConfig.inbounds.Clear();
            _coreConfig.outbounds.Clear();
            _coreConfig.routing.rules.Clear();

            var initPort = AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest);

            foreach (var it in selecteds)
            {
                if (!Global.XraySupportConfigType.Contains(it.ConfigType))
                {
                    continue;
                }
                if (it.Port <= 0)
                {
                    continue;
                }
                var actIndexId = context.ServerTestItemMap.GetValueOrDefault(it.IndexId, it.IndexId);
                var item = context.AllProxiesMap.GetValueOrDefault(actIndexId);
                if (item is null || item.ConfigType is EConfigType.Custom || !item.IsValid())
                {
                    continue;
                }

                //find unused port
                var port = initPort;
                for (var k = initPort; k < Global.MaxPort; k++)
                {
                    if (lstIpEndPoints?.FindIndex(_it => _it.Port == k) >= 0)
                    {
                        continue;
                    }
                    if (lstTcpConns?.FindIndex(_it => _it.LocalEndPoint.Port == k) >= 0)
                    {
                        continue;
                    }
                    //found
                    port = k;
                    initPort = port + 1;
                    break;
                }

                //Port In Used
                if (lstIpEndPoints?.FindIndex(_it => _it.Port == port) >= 0)
                {
                    continue;
                }
                it.Port = port;
                it.AllowTest = true;

                //inbound
                Inbounds4Ray inbound = new()
                {
                    listen = Global.Loopback,
                    port = port,
                    protocol = EInboundProtocol.mixed.ToString(),
                };
                inbound.tag = inbound.protocol + inbound.port.ToString();
                _coreConfig.inbounds.Add(inbound);

                var tag = Global.ProxyTag + inbound.port.ToString();
                var isBalancer = false;
                //outbound
                var proxyOutbounds =
                    new CoreConfigV2rayService(context with { Node = item }).BuildAllProxyOutbounds(tag);
                _coreConfig.outbounds.AddRange(proxyOutbounds);
                if (proxyOutbounds.Count(n => n.tag.StartsWith(tag)) > 1)
                {
                    isBalancer = true;
                    var multipleLoad = _node.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
                    GenObservatory(multipleLoad, tag);
                    GenBalancer(multipleLoad, tag);
                }

                //rule
                RulesItem4Ray rule = new()
                {
                    inboundTag = new List<string> { inbound.tag },
                    outboundTag = tag,
                    type = "field"
                };
                if (isBalancer)
                {
                    rule.balancerTag = tag;
                    rule.outboundTag = null;
                }
                _coreConfig.routing.rules.Add(rule);
            }

            //ret.Msg =string.Format(ResUI.SuccessfulConfiguration"), node.getSummary());
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(_coreConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public RetResult GenerateClientSpeedtestConfig(int port)
    {
        var ret = new RetResult();
        try
        {
            if (_node == null
                || !_node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (_node.GetNetwork() is nameof(ETransport.quic))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {_node.GetNetwork()}";
                return ret;
            }

            var result = EmbedUtils.GetEmbedText(Global.V2raySampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (_coreConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            GenLog();
            GenOutbounds();

            _coreConfig.routing.domainStrategy = Global.AsIs;
            _coreConfig.routing.rules.Clear();
            _coreConfig.inbounds.Clear();
            _coreConfig.inbounds.Add(new()
            {
                tag = $"{EInboundProtocol.socks}{port}",
                listen = Global.Loopback,
                port = port,
                protocol = EInboundProtocol.mixed.ToString(),
            });

            _coreConfig.routing.rules.Add(BuildFinalRule());

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(_coreConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public RetResult GenerateClientProxyRelayConfig()
    {
        var ret = new RetResult();
        try
        {
            if (_node == null
                || !_node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (_node.GetNetwork() is nameof(ETransport.quic))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {_node.GetNetwork()}";
                return ret;
            }

            var result = EmbedUtils.GetEmbedText(Global.V2raySampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (_coreConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            GenLog();
            _coreConfig.outbounds.Clear();
            GenOutbounds();

            var protectNode = new ProfileItem()
            {
                CoreType = ECoreType.Xray,
                ConfigType = EConfigType.Shadowsocks,
                Address = Global.Loopback,
                Port = context.TunProtectSsPort,
                Password = Global.None,
            };
            protectNode.SetProtocolExtra(protectNode.GetProtocolExtra() with
            {
                SsMethod = Global.None,
            });

            foreach (var outbound in _coreConfig.outbounds.Where(outbound => outbound.streamSettings?.sockopt?.dialerProxy?.IsNullOrEmpty() ?? true))
            {
                outbound.streamSettings ??= new StreamSettings4Ray();
                outbound.streamSettings.sockopt ??= new Sockopt4Ray();
                outbound.streamSettings.sockopt.dialerProxy = "tun-project-ss";
            }
            _coreConfig.outbounds.Add(new CoreConfigV2rayService(context with
            {
                Node = protectNode,
            }).BuildProxyOutbound("tun-project-ss"));

            var hasBalancer = _coreConfig.routing.balancers is { Count: > 0 };
            _coreConfig.routing.rules =
            [
                new()
                {
                    inboundTag = new List<string> { "proxy-relay-ss" },
                    outboundTag = hasBalancer ? null : Global.ProxyTag,
                    balancerTag = hasBalancer ? Global.ProxyTag + Global.BalancerTagSuffix: null,
                    type = "field"
                }
            ];
            _coreConfig.inbounds.Clear();

            var configNode = JsonUtils.ParseJson(JsonUtils.Serialize(_coreConfig))!;
            configNode["inbounds"]!.AsArray().Add(new
            {
                listen = Global.Loopback,
                port = context.ProxyRelaySsPort,
                protocol = "shadowsocks",
                settings = new
                {
                    network = "tcp,udp",
                    method = Global.None,
                    password = Global.None,
                },
                tag = "proxy-relay-ss",
            });

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(configNode);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    #endregion public gen function
}
