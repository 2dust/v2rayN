namespace v2rayN.Mode;

[Serializable]
public class ConfigOld
{
    #region property

    /// <summary>
    /// 允许日志
    /// </summary>
    public bool logEnabled
    {
        get; set;
    }

    /// <summary>
    /// 日志等级
    /// </summary>
    public string loglevel
    {
        get; set;
    }

    public string indexId
    {
        get; set;
    }

    /// <summary>
    /// 允许Mux多路复用
    /// </summary>
    public bool muxEnabled
    {
        get; set;
    }

    /// <summary>
    ///
    /// </summary>
    public ESysProxyType sysProxyType
    {
        get; set;
    }

    /// <summary>
    /// 启用实时网速和流量统计
    /// </summary>
    public bool enableStatistics
    {
        get; set;
    }

    /// <summary>
    /// 去重时优先保留较旧（顶部）节点
    /// </summary>
    public bool keepOlderDedupl
    {
        get; set;
    }

    /// <summary>
    /// 视图刷新率
    /// </summary>
    public int statisticsFreshRate
    {
        get; set;
    }

    /// <summary>
    /// 自定义远程DNS
    /// </summary>
    public string remoteDNS
    {
        get; set;
    }

    /// <summary>
    /// Outbound Freedom domainStrategy
    /// </summary>
    public string domainStrategy4Freedom
    {
        get; set;
    }

    /// <summary>
    /// 是否允许不安全连接
    /// </summary>
    public bool defAllowInsecure
    {
        get; set;
    }

    /// <summary>
    /// 域名解析策略
    /// </summary>
    public string domainStrategy
    {
        get; set;
    }

    public string domainMatcher
    {
        get; set;
    }

    public int routingIndex
    {
        get; set;
    }

    public bool enableRoutingAdvanced
    {
        get; set;
    }

    public bool ignoreGeoUpdateCore
    {
        get; set;
    }

    /// <summary>
    /// systemProxyExceptions
    /// </summary>
    public string systemProxyExceptions
    {
        get; set;
    }

    public string systemProxyAdvancedProtocol { get; set; }

    public int autoUpdateInterval { get; set; } = 0;

    public int autoUpdateSubInterval { get; set; } = 0;

    public bool checkPreReleaseUpdate { get; set; } = false;

    public bool enableSecurityProtocolTls13
    {
        get; set;
    }

    public int trayMenuServersLimit { get; set; }

    #endregion property

    #region other entities

    /// <summary>
    /// 本地监听
    /// </summary>
    public List<InItem> inbound
    {
        get; set;
    }

    /// <summary>
    /// vmess服务器信息
    /// </summary>
    public List<VmessItem> vmess
    {
        get; set;
    }

    /// <summary>
    /// KcpItem
    /// </summary>
    public KcpItem kcpItem
    {
        get; set;
    }

    /// <summary>
    /// 订阅
    /// </summary>
    public List<SubItem> subItem
    {
        get; set;
    }

    /// <summary>
    /// UI
    /// </summary>
    public UIItem uiItem
    {
        get; set;
    }

    public List<RoutingItemOld> routings
    {
        get; set;
    }

    public ConstItem constItem
    {
        get; set;
    }

    public List<KeyEventItem> globalHotkeys
    {
        get; set;
    }

    public List<CoreTypeItem> coreTypeItem
    {
        get; set;
    }

    #endregion other entities
}

[Serializable]
public class VmessItem
{
    public VmessItem()
    {
        indexId = string.Empty;
        configType = EConfigType.VMess;
        configVersion = 2;
        sort = 0;
        address = string.Empty;
        port = 0;
        id = string.Empty;
        alterId = 0;
        security = string.Empty;
        network = string.Empty;
        remarks = string.Empty;
        headerType = string.Empty;
        requestHost = string.Empty;
        path = string.Empty;
        streamSecurity = string.Empty;
        allowInsecure = string.Empty;
        testResult = string.Empty;
        subid = string.Empty;
        flow = string.Empty;
        groupId = string.Empty;
    }

    public string indexId
    {
        get; set;
    }

    /// <summary>
    /// config type(1=normal,2=custom)
    /// </summary>
    public EConfigType configType
    {
        get; set;
    }

    /// <summary>
    /// 版本(现在=2)
    /// </summary>
    public int configVersion
    {
        get; set;
    }

    public int sort
    {
        get; set;
    }

    /// <summary>
    /// 远程服务器地址
    /// </summary>
    public string address
    {
        get; set;
    }

    /// <summary>
    /// 远程服务器端口
    /// </summary>
    public int port
    {
        get; set;
    }

    /// <summary>
    /// 远程服务器ID
    /// </summary>
    public string id
    {
        get; set;
    }

    /// <summary>
    /// 远程服务器额外ID
    /// </summary>
    public int alterId
    {
        get; set;
    }

    /// <summary>
    /// 本地安全策略
    /// </summary>
    public string security
    {
        get; set;
    }

    /// <summary>
    /// tcp,kcp,ws,h2,quic
    /// </summary>
    public string network
    {
        get; set;
    }

    /// <summary>
    ///
    /// </summary>
    public string remarks
    {
        get; set;
    }

    /// <summary>
    /// 伪装类型
    /// </summary>
    public string headerType
    {
        get; set;
    }

    /// <summary>
    /// 伪装的域名
    /// </summary>
    public string requestHost
    {
        get; set;
    }

    /// <summary>
    /// ws h2 path
    /// </summary>
    public string path
    {
        get; set;
    }

    /// <summary>
    /// 传输层安全
    /// </summary>
    public string streamSecurity
    {
        get; set;
    }

    /// <summary>
    /// 是否允许不安全连接（用于客户端）
    /// </summary>
    public string allowInsecure
    {
        get; set;
    }

    /// <summary>
    ///
    /// </summary>
    public string testResult
    {
        get; set;
    }

    /// <summary>
    /// SubItem id
    /// </summary>
    public string subid
    {
        get; set;
    }

    /// <summary>
    /// VLESS flow
    /// </summary>
    public string flow
    {
        get; set;
    }

    /// <summary>
    /// tls sni
    /// </summary>
    public string sni
    {
        get; set;
    }

    public string groupId
    {
        get; set;
    } = string.Empty;

    public ECoreType? coreType
    {
        get; set;
    }

    public int preSocksPort
    {
        get; set;
    }

    public string fingerprint { get; set; }
}

[Serializable]
public class RoutingItemOld
{
    public string remarks
    {
        get; set;
    }

    public string url
    {
        get; set;
    }

    public List<RulesItem> rules
    {
        get; set;
    }

    public bool enabled { get; set; } = true;

    public bool locked
    {
        get; set;
    }

    public string customIcon
    {
        get; set;
    }
}