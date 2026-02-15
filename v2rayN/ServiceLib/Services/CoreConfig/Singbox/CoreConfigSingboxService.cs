namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService(CoreConfigContext context)
{
    private static readonly string _tag = "CoreConfigSingboxService";
    private readonly Config _config = context.AppConfig;
    private readonly ProfileItem _node = context.Node;

    private SingboxConfig _coreConfig = new();

    #region public gen function

    public RetResult GenerateClientConfigContent()
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
            if (_node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {_node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<SingboxConfig>(result);
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

            GenExperimental();

            ConvertGeo2Ruleset();

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;

            ret.Data = ApplyFullConfigTemplate();
            if (context.TunProtectSsPort is > 0 and <= 65535)
            {
                var ssInbound = new
                {
                    type = "shadowsocks",
                    tag = "tun-protect-ss",
                    listen = Global.Loopback,
                    listen_port = context.TunProtectSsPort,
                    method = "none",
                    password = "none",
                };
                var directRule = new Rule4Sbox()
                {
                    inbound = new List<string> { ssInbound.tag },
                    outbound = Global.DirectTag,
                };
                var singboxConfigNode = JsonUtils.ParseJson(ret.Data.ToString())!.AsObject();
                var inboundsNode = singboxConfigNode["inbounds"]!.AsArray();
                inboundsNode.Add(JsonUtils.SerializeToNode(ssInbound, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }));
                var routeNode = singboxConfigNode["route"]?.AsObject();
                var rulesNode = routeNode?["rules"]?.AsArray();
                var protectRuleNode = JsonUtils.SerializeToNode(directRule,
                    new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                if (rulesNode != null)
                {
                    rulesNode.Insert(0, protectRuleNode);
                }
                else
                {
                    var newRulesNode = new JsonArray() { protectRuleNode };
                    if (routeNode is null)
                    {
                        var newRouteNode = new JsonObject() { ["rules"] = newRulesNode };
                        singboxConfigNode["route"] = newRouteNode;
                    }
                    else
                    {
                        routeNode["rules"] = newRulesNode;
                    }
                }
                ret.Data = JsonUtils.Serialize(singboxConfigNode);
            }
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

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (_coreConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            List<IPEndPoint> lstIpEndPoints = new();
            List<TcpConnectionInformation> lstTcpConns = new();
            try
            {
                lstIpEndPoints.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners());
                lstIpEndPoints.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners());
                lstTcpConns.AddRange(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections());
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }

            GenLog();
            GenMinimizedDns();
            _coreConfig.inbounds.Clear();
            _coreConfig.outbounds.RemoveAt(0);

            var initPort = AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest);

            foreach (var it in selecteds)
            {
                if (!Global.SingboxSupportConfigType.Contains(it.ConfigType))
                {
                    continue;
                }
                if (it.Port <= 0)
                {
                    continue;
                }
                var item = context.AllProxiesMap.GetValueOrDefault(it.IndexId);
                if (item is null || item.IsComplex() || !item.IsValid())
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
                Inbound4Sbox inbound = new()
                {
                    listen = Global.Loopback,
                    listen_port = port,
                    type = EInboundProtocol.mixed.ToString(),
                };
                inbound.tag = inbound.type + inbound.listen_port.ToString();
                _coreConfig.inbounds.Add(inbound);

                var tag = Global.ProxyTag + inbound.listen_port.ToString();
                var serverList = new CoreConfigSingboxService(context with { Node = item }).BuildAllProxyOutbounds(tag);
                FillRangeProxy(serverList, _coreConfig, false);

                //rule
                Rule4Sbox rule = new()
                {
                    inbound = new List<string> { inbound.tag },
                    outbound = tag
                };
                _coreConfig.route.rules.Add(rule);
            }

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
            if (_node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {_node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            _coreConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (_coreConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            GenLog();
            GenOutbounds();
            GenMinimizedDns();

            _coreConfig.route.rules.Clear();
            _coreConfig.inbounds.Clear();
            _coreConfig.inbounds.Add(new()
            {
                tag = $"{EInboundProtocol.mixed}{port}",
                listen = Global.Loopback,
                listen_port = port,
                type = EInboundProtocol.mixed.ToString(),
            });

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

    #endregion public gen function
}
