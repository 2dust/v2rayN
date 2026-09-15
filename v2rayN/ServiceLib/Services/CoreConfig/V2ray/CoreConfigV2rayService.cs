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

            if (_config.CoreBasicItem.EnableFragment)
            {
                ApplyOutboundFragment();
            }
            if (_config.CoreBasicItem.EnableFinalFragment)
            {
                ApplyFinalFragment();
            }

            var finalRule = BuildFinalRule();
            if (!string.IsNullOrEmpty(finalRule?.balancerTag))
            {
                _coreConfig.routing.rules.Add(finalRule);
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = ApplyFinalConfigModifiers();
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
                if (!(Global.XraySupportConfigType.Contains(it.ConfigType) || it.ConfigType.IsGroupType() || it.ConfigType is EConfigType.Outbound))
                {
                    continue;
                }
                if (!it.ConfigType.IsComplexType() && it.Port <= 0)
                {
                    continue;
                }
                var actIndexId = context.ServerTestItemMap.GetValueOrDefault(it.IndexId, it.IndexId);
                var item = context.AllProxiesMap.GetValueOrDefault(actIndexId);
                if (item is null || item.ConfigType is EConfigType.Custom || !item.IsValid())
                {
                    continue;
                }

                // Xray rejects the entire config when it contains a plaintext VLESS
                // outbound to a public IP ("vless without TLS or other encryption is
                // prohibited unless the server address is a private IP or domain"),
                // which would fail the whole batch with -1. Skip such nodes here so
                // the rest of the batch can still be tested; they keep
                // AllowTest=false and are reported as skipped.
                if (IsVlessPlaintextToPublicIp(item))
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
                    protocol = nameof(EInboundProtocol.mixed),
                    settings = new Inboundsettings4Ray()
                    {
                        udp = true,
                        auth = "noauth"
                    },
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
                    inboundTag = [inbound.tag],
                    outboundTag = tag,
                    type = "field"
                };
                if (isBalancer)
                {
                    rule.balancerTag = tag + Global.BalancerTagSuffix;
                    rule.outboundTag = null;
                }
                _coreConfig.routing.rules.Add(rule);
            }

            if (_config.CoreBasicItem.EnableFragment)
            {
                ApplyOutboundFragment();
            }
            if (_config.CoreBasicItem.EnableFinalFragment)
            {
                ApplyFinalFragment();
            }
            ApplyOutboundBindInterface();
            ApplyOutboundSendThrough();
            //ret.Msg =string.Format(ResUI.SuccessfulConfiguration"), node.getSummary());
            ret.Success = true;
            ret.Data = ApplyCustomOutboundReplace();
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
                protocol = nameof(EInboundProtocol.mixed),
                settings = new Inboundsettings4Ray()
                {
                    udp = true,
                    auth = "noauth"
                },
            });

            _coreConfig.routing.rules.Add(BuildFinalRule());

            if (_config.CoreBasicItem.EnableFragment)
            {
                ApplyOutboundFragment();
            }
            if (_config.CoreBasicItem.EnableFinalFragment)
            {
                ApplyFinalFragment();
            }
            ApplyOutboundBindInterface();
            ApplyOutboundSendThrough();

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            ret.Data = ApplyCustomOutboundReplace();
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

    /// <summary>
    /// Xray refuses to start when a VLESS outbound has no TLS/Reality and no
    /// other encryption to a public IP. In a batched speedtest all nodes share
    /// one core config, so one such node fails the whole batch. Detect it here
    /// so callers can skip the node instead of poisoning the batch.
    /// </summary>
    public static bool IsVlessPlaintextToPublicIp(ProfileItem item)
    {
        if (item.ConfigType != EConfigType.VLESS)
        {
            return false;
        }

        if (item.StreamSecurity is Global.StreamSecurity or Global.StreamSecurityReality)
        {
            return false;
        }

        var encryption = item.GetProtocolExtra().VlessEncryption.TrimEx();
        if (!encryption.IsNullOrEmpty()
            && !encryption.Equals(Global.None, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var address = item.Address.TrimEx();
        if (address.IsNullOrEmpty())
        {
            return false;
        }

        // Xray only allows plaintext VLESS to private IPs or domains.
        if (!Utils.IsIpAddress(address))
        {
            return false;
        }

        return !Utils.IsPrivateNetwork(address);
    }
}
