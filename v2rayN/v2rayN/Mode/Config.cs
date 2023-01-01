namespace v2rayN.Mode
{
    /// <summary>
    /// 本软件配置文件实体类
    /// </summary>
    [Serializable]
    public class Config
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
        public string routingIndexId { get; set; }
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

        public bool autoHideStartup { get; set; }

        #endregion

        #region other entities

        public TunModeItem tunModeItem { get; set; }
        public KcpItem kcpItem { get; set; }
        public GrpcItem grpcItem { get; set; }
        public UIItem uiItem { get; set; }
        public ConstItem constItem { get; set; }
        public List<InItem> inbound { get; set; }
        public List<KeyEventItem> globalHotkeys { get; set; }
        public List<CoreTypeItem> coreTypeItem { get; set; }

        #endregion

    }
}
