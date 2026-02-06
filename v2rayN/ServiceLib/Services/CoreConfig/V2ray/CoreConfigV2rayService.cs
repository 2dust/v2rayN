namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService(CoreConfigContext context)
{
    private static readonly string _tag = "CoreConfigV2rayService";

    private V2rayConfig _coreConfig = new();

    #region public gen function

    public RetResult GenerateClientConfigContent()
    {
        var ret = new RetResult();
        try
        {
            var node = context?.Node;
            if (node == null
                || !node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (node.GetNetwork() is nameof(ETransport.quic))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
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

            var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (v2rayConfig == null)
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
            v2rayConfig.inbounds.Clear();
            v2rayConfig.outbounds.Clear();
            v2rayConfig.routing.rules.Clear();

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
                Inbounds4Ray inbound = new()
                {
                    listen = Global.Loopback,
                    port = port,
                    protocol = EInboundProtocol.mixed.ToString(),
                };
                inbound.tag = inbound.protocol + inbound.port.ToString();
                v2rayConfig.inbounds.Add(inbound);

                var tag = Global.ProxyTag + inbound.port.ToString();
                var isBalancer = false;
                //outbound
                var proxyOutbounds = BuildAllProxyOutbounds(tag);
                v2rayConfig.outbounds.AddRange(proxyOutbounds);
                if (proxyOutbounds.Count(n => n.tag.StartsWith(tag)) > 1)
                {
                    isBalancer = true;
                    var multipleLoad = context.Node.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
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
                v2rayConfig.routing.rules.Add(rule);
            }

            //ret.Msg =string.Format(ResUI.SuccessfulConfiguration"), node.getSummary());
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(v2rayConfig);
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
            var node = context.Node;
            if (node == null
                || !node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (node.GetNetwork() is nameof(ETransport.quic))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
                return ret;
            }

            var result = EmbedUtils.GetEmbedText(Global.V2raySampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (v2rayConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            GenLog();
            GenOutbounds();

            v2rayConfig.routing.rules.Clear();
            v2rayConfig.inbounds.Clear();
            v2rayConfig.inbounds.Add(new()
            {
                tag = $"{EInboundProtocol.socks}{port}",
                listen = Global.Loopback,
                port = port,
                protocol = EInboundProtocol.mixed.ToString(),
            });

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(v2rayConfig);
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
