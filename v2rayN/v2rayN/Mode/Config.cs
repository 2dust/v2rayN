namespace v2rayN.Mode
{
    /// <summary>
    /// 本软件配置文件实体类
    /// </summary>
    [Serializable]
    public class Config
    {
        #region property

      

        public string indexId
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

        public bool autoRun { get; set; }

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

        public bool ignoreGeoUpdateCore { get; set; } = true;

        /// <summary>
        /// systemProxyExceptions
        /// </summary>
        public string systemProxyExceptions
        {
            get; set;
        }
        public string systemProxyAdvancedProtocol { get; set; }

        public int autoUpdateInterval { get; set; } = 10;

        public int autoUpdateSubInterval { get; set; } = 10;

        public bool checkPreReleaseUpdate { get; set; } = false;

        public bool enableSecurityProtocolTls13
        {
            get; set;
        }

        public int trayMenuServersLimit { get; set; } = 20;

        #endregion

        #region other entities

        public CoreBasicItem coreBasicItem { get; set; }
        public TunModeItem tunModeItem { get; set; }
        public KcpItem kcpItem { get; set; }
        public GrpcItem grpcItem { get; set; }
        public UIItem uiItem { get; set; }
        public ConstItem constItem { get; set; }
        public SpeedTestItem speedTestItem { get; set; }
        public List<InItem> inbound { get; set; }
        public List<KeyEventItem> globalHotkeys { get; set; }
        public List<CoreTypeItem> coreTypeItem { get; set; }

        #endregion

    }
}
