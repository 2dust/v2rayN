namespace ServiceLib.Models;

/// <summary>
/// Core launch context that encapsulates all parameters required for launching
/// </summary>
public class CoreLaunchContext
{
    public ProfileItem Node { get; set; }
    public bool SplitCore { get; set; }
    public ECoreType CoreType { get; set; }
    public ECoreType? PreCoreType { get; set; }
    public ECoreType PureEndpointCore { get; set; }
    public ECoreType SplitRouteCore { get; set; }
    public bool EnableTun { get; set; }
    public int PreSocksPort { get; set; }
    public EConfigType ConfigType { get; set; }

    public CoreLaunchContext(ProfileItem node, Config config)
    {
        Node = node;
        SplitCore = config.SplitCoreItem.EnableSplitCore;
        CoreType = AppHandler.Instance.GetCoreType(node, node.ConfigType);
        PureEndpointCore = AppHandler.Instance.GetSplitCoreType(node, node.ConfigType);
        SplitRouteCore = config.SplitCoreItem.RouteCoreType;
        EnableTun = config.TunModeItem.EnableTun;
        PreSocksPort = 0;
        PreCoreType = null;
        ConfigType = node.ConfigType;
    }

    /// <summary>
    /// Adjust context parameters based on configuration type
    /// </summary>
    public void AdjustForConfigType()
    {
        (SplitCore, CoreType, PreCoreType) = AppHandler.Instance.GetCoreAndPreType(Node);
        if (Node.ConfigType == EConfigType.Custom)
        {
            if (Node.PreSocksPort > 0)
            {
                PreSocksPort = Node.PreSocksPort.Value;
            }
            else
            {
                EnableTun = false;
            }
        }
        else if (PreCoreType != null)
        {
            PreSocksPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.split);
        }
    }
}
