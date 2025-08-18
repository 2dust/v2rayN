using System.Net;
using System.Net.NetworkInformation;

namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService(Config config)
{
    private readonly Config _config = config;
    private static readonly string _tag = "CoreConfigV2rayService";

    #region public gen function

    public async Task<RetResult> GenerateClientConfigContent(ProfileItem node)
    {
        var ret = new RetResult();
        try
        {
            if (node == null
                || node.Port <= 0)
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

            var v2rayConfig = JsonUtils.Deserialize<V2rayConfig>(result);
            if (v2rayConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenLog(v2rayConfig);

            await GenInbounds(v2rayConfig);

            await GenOutbound(node, v2rayConfig.outbounds.First());

            await GenMoreOutbounds(node, v2rayConfig);

            await GenRouting(v2rayConfig);

            await GenDns(node, v2rayConfig);

            await GenStatistic(v2rayConfig);

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = await ApplyFullConfigTemplate(v2rayConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientMultipleLoadConfig(List<ProfileItem> selecteds, EMultipleLoad multipleLoad)
    {
        var ret = new RetResult();

        try
        {
            if (_config == null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            string result = EmbedUtils.GetEmbedText(Global.V2raySampleClient);
            string txtOutbound = EmbedUtils.GetEmbedText(Global.V2raySampleOutbound);
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

            await GenLog(v2rayConfig);
            await GenInbounds(v2rayConfig);
            await GenRouting(v2rayConfig);
            await GenDns(null, v2rayConfig);
            await GenStatistic(v2rayConfig);
            v2rayConfig.outbounds.RemoveAt(0);

            var proxyProfiles = new List<ProfileItem>();
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
                var item = await AppManager.Instance.GetProfileItem(it.IndexId);
                if (item is null)
                {
                    continue;
                }
                if (it.ConfigType is EConfigType.VMess or EConfigType.VLESS)
                {
                    if (item.Id.IsNullOrEmpty() || !Utils.IsGuidByParse(item.Id))
                    {
                        continue;
                    }
                }
                if (item.ConfigType == EConfigType.Shadowsocks
                  && !Global.SsSecuritiesInSingbox.Contains(item.Security))
                {
                    continue;
                }
                if (item.ConfigType == EConfigType.VLESS && !Global.Flows.Contains(item.Flow))
                {
                    continue;
                }

                //outbound
                proxyProfiles.Add(item);
            }
            if (proxyProfiles.Count <= 0)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            await GenOutboundsList(proxyProfiles, v2rayConfig);

            //add balancers
            await GenBalancer(v2rayConfig, multipleLoad);

            var balancer = v2rayConfig.routing.balancers.First();

            //add rule
            var rules = v2rayConfig.routing.rules.Where(t => t.outboundTag == Global.ProxyTag).ToList();
            if (rules?.Count > 0)
            {
                foreach (var rule in rules)
                {
                    rule.outboundTag = null;
                    rule.balancerTag = balancer.tag;
                }
            }
            if (v2rayConfig.routing.domainStrategy == Global.IPIfNonMatch)
            {
                v2rayConfig.routing.rules.Add(new()
                {
                    ip = ["0.0.0.0/0", "::/0"],
                    balancerTag = balancer.tag,
                    type = "field"
                });
            }
            else
            {
                v2rayConfig.routing.rules.Add(new()
                {
                    network = "tcp,udp",
                    balancerTag = balancer.tag,
                    type = "field"
                });
            }

            ret.Success = true;

            ret.Data = await ApplyFullConfigTemplate(v2rayConfig, true);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientSpeedtestConfig(List<ServerTestItem> selecteds)
    {
        var ret = new RetResult();
        try
        {
            if (_config == null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

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

            await GenLog(v2rayConfig);
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
                var item = await AppManager.Instance.GetProfileItem(it.IndexId);
                if (it.ConfigType is EConfigType.VMess or EConfigType.VLESS)
                {
                    if (item is null || item.Id.IsNullOrEmpty() || !Utils.IsGuidByParse(item.Id))
                    {
                        continue;
                    }
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

                //outbound
                if (item is null)
                {
                    continue;
                }
                if (item.ConfigType == EConfigType.Shadowsocks
                    && !Global.SsSecuritiesInXray.Contains(item.Security))
                {
                    continue;
                }
                if (item.ConfigType == EConfigType.VLESS
                 && !Global.Flows.Contains(item.Flow))
                {
                    continue;
                }
                if (it.ConfigType is EConfigType.VLESS or EConfigType.Trojan
                    && item.StreamSecurity == Global.StreamSecurityReality
                    && item.PublicKey.IsNullOrEmpty())
                {
                    continue;
                }

                //inbound
                Inbounds4Ray inbound = new()
                {
                    listen = Global.Loopback,
                    port = port,
                    protocol = EInboundProtocol.mixed.ToString(),
                };
                inbound.tag = inbound.protocol + inbound.port.ToString();
                v2rayConfig.inbounds.Add(inbound);

                var outbound = JsonUtils.Deserialize<Outbounds4Ray>(txtOutbound);
                await GenOutbound(item, outbound);
                outbound.tag = Global.ProxyTag + inbound.port.ToString();
                v2rayConfig.outbounds.Add(outbound);

                //rule
                RulesItem4Ray rule = new()
                {
                    inboundTag = new List<string> { inbound.tag },
                    outboundTag = outbound.tag,
                    type = "field"
                };
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

    public async Task<RetResult> GenerateClientSpeedtestConfig(ProfileItem node, int port)
    {
        var ret = new RetResult();
        try
        {
            if (node is not { Port: > 0 })
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

            await GenLog(v2rayConfig);
            await GenOutbound(node, v2rayConfig.outbounds.First());
            await GenMoreOutbounds(node, v2rayConfig);

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
