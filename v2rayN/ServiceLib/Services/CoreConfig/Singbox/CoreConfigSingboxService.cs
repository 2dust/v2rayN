using System.Net;
using System.Net.NetworkInformation;
using ServiceLib.Common;

namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService(Config config)
{
    private readonly Config _config = config;
    private static readonly string _tag = "CoreConfigSingboxService";

    #region public gen function

    public async Task<RetResult> GenerateClientConfigContent(ProfileItem node)
    {
        var ret = new RetResult();
        try
        {
            if (node == null
                || !node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }
            if (node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            if (node.ConfigType.IsGroupType())
            {
                switch (node.ConfigType)
                {
                    case EConfigType.PolicyGroup:
                        return await GenerateClientMultipleLoadConfig(node);

                    case EConfigType.ProxyChain:
                        return await GenerateClientChainConfig(node);
                }
            }

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenLog(singboxConfig);

            await GenInbounds(singboxConfig);

            if (node.ConfigType == EConfigType.WireGuard)
            {
                singboxConfig.outbounds.RemoveAt(0);
                var endpoints = new Endpoints4Sbox();
                await GenEndpoint(node, endpoints);
                endpoints.tag = Global.ProxyTag;
                singboxConfig.endpoints = new() { endpoints };
            }
            else
            {
                await GenOutbound(node, singboxConfig.outbounds.First());
            }

            await GenMoreOutbounds(node, singboxConfig);

            await GenRouting(singboxConfig);

            await GenDns(node, singboxConfig);

            await GenExperimental(singboxConfig);

            await ConvertGeo2Ruleset(singboxConfig);

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;

            ret.Data = await ApplyFullConfigTemplate(singboxConfig);
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

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
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

            await GenLog(singboxConfig);
            //GenDns(new(), singboxConfig);
            singboxConfig.inbounds.Clear();
            singboxConfig.outbounds.RemoveAt(0);

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
                var item = await AppManager.Instance.GetProfileItem(it.IndexId);
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
                singboxConfig.inbounds.Add(inbound);

                //outbound
                var server = await GenServer(item);
                if (server is null)
                {
                    ret.Msg = ResUI.FailedGenDefaultConfiguration;
                    return ret;
                }
                var tag = Global.ProxyTag + inbound.listen_port.ToString();
                server.tag = tag;
                if (server is Endpoints4Sbox endpoint)
                {
                    singboxConfig.endpoints ??= new();
                    singboxConfig.endpoints.Add(endpoint);
                }
                else if (server is Outbound4Sbox outbound)
                {
                    singboxConfig.outbounds.Add(outbound);
                }

                //rule
                Rule4Sbox rule = new()
                {
                    inbound = new List<string> { inbound.tag },
                    outbound = tag
                };
                singboxConfig.route.rules.Add(rule);
            }

            var rawDNSItem = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
            if (rawDNSItem != null && rawDNSItem.Enabled == true)
            {
                await GenDnsDomainsCompatible(singboxConfig, rawDNSItem);
            }
            else
            {
                await GenDnsDomains(singboxConfig, _config.SimpleDNSItem);
            }
            singboxConfig.route.default_domain_resolver = new()
            {
                server = Global.SingboxLocalDNSTag,
            };

            ret.Success = true;
            ret.Data = JsonUtils.Serialize(singboxConfig);
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
            if (node == null
                || !node.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }
            if (node.GetNetwork() is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
            {
                ret.Msg = ResUI.Incorrectconfiguration + $" - {node.GetNetwork()}";
                return ret;
            }

            ret.Msg = ResUI.InitialConfiguration;

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            if (result.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenLog(singboxConfig);
            if (node.ConfigType == EConfigType.WireGuard)
            {
                singboxConfig.outbounds.RemoveAt(0);
                var endpoints = new Endpoints4Sbox();
                await GenEndpoint(node, endpoints);
                endpoints.tag = Global.ProxyTag;
                singboxConfig.endpoints = new() { endpoints };
            }
            else
            {
                await GenOutbound(node, singboxConfig.outbounds.First());
            }
            await GenMoreOutbounds(node, singboxConfig);
            var item = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
            if (item != null && item.Enabled == true)
            {
                await GenDnsDomainsCompatible(singboxConfig, item);
            }
            else
            {
                await GenDnsDomains(singboxConfig, _config.SimpleDNSItem);
            }
            singboxConfig.route.default_domain_resolver = new()
            {
                server = Global.SingboxLocalDNSTag,
            };

            singboxConfig.route.rules.Clear();
            singboxConfig.inbounds.Clear();
            singboxConfig.inbounds.Add(new()
            {
                tag = $"{EInboundProtocol.mixed}{port}",
                listen = Global.Loopback,
                listen_port = port,
                type = EInboundProtocol.mixed.ToString(),
            });

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = JsonUtils.Serialize(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientMultipleLoadConfig(ProfileItem parentNode)
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

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            singboxConfig.outbounds.RemoveAt(0);

            await GenLog(singboxConfig);
            await GenInbounds(singboxConfig);

            var groupRet = await GenGroupOutbound(parentNode, singboxConfig);
            if (groupRet != 0)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenRouting(singboxConfig);
            await GenExperimental(singboxConfig);
            await GenDns(null, singboxConfig);
            await ConvertGeo2Ruleset(singboxConfig);

            ret.Success = true;

            ret.Data = await ApplyFullConfigTemplate(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientChainConfig(ProfileItem parentNode)
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

            var result = EmbedUtils.GetEmbedText(Global.SingboxSampleClient);
            var txtOutbound = EmbedUtils.GetEmbedText(Global.SingboxSampleOutbound);
            if (result.IsNullOrEmpty() || txtOutbound.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }

            var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(result);
            if (singboxConfig == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            singboxConfig.outbounds.RemoveAt(0);

            await GenLog(singboxConfig);
            await GenInbounds(singboxConfig);

            var groupRet = await GenGroupOutbound(parentNode, singboxConfig);
            if (groupRet != 0)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            await GenRouting(singboxConfig);
            await GenExperimental(singboxConfig);
            await GenDns(null, singboxConfig);
            await ConvertGeo2Ruleset(singboxConfig);

            ret.Success = true;

            ret.Data = await ApplyFullConfigTemplate(singboxConfig);
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    public async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        if (node == null || fileName is null)
        {
            ret.Msg = ResUI.CheckServerSettings;
            return ret;
        }

        ret.Msg = ResUI.InitialConfiguration;

        try
        {
            if (node == null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            var addressFileName = node.Address;
            if (addressFileName.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }
            if (!File.Exists(addressFileName))
            {
                addressFileName = Path.Combine(Utils.GetConfigPath(), addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "1";
                return ret;
            }

            if (node.Address == Global.CoreMultipleLoadConfigFileName)
            {
                var txtFile = File.ReadAllText(addressFileName);
                var singboxConfig = JsonUtils.Deserialize<SingboxConfig>(txtFile);
                if (singboxConfig == null)
                {
                    File.Copy(addressFileName, fileName);
                }
                else
                {
                    await GenInbounds(singboxConfig);
                    await GenExperimental(singboxConfig);

                    var content = JsonUtils.Serialize(singboxConfig, true);
                    await File.WriteAllTextAsync(fileName, content);
                }
            }
            else
            {
                File.Copy(addressFileName, fileName);
            }

            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "2";
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
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
