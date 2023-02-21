namespace v2rayN.Mode
{
    /// <summary>
    /// 本软件配置文件实体类
    /// </summary>
    [Serializable]
    public class Config
    {
        #region property

        public string indexId { get; set; }
        public string subIndexId { get; set; }

        public string remoteDNS { get; set; }
        /// <summary>
        /// Outbound Freedom domainStrategy
        /// </summary>
        public string domainStrategy4Freedom { get; set; }

        public ESysProxyType sysProxyType { get; set; }
        public string systemProxyExceptions { get; set; }
        public string systemProxyAdvancedProtocol { get; set; }

        #endregion

        #region other entities

        public CoreBasicItem coreBasicItem { get; set; }
        public TunModeItem tunModeItem { get; set; }
        public KcpItem kcpItem { get; set; }
        public GrpcItem grpcItem { get; set; }
        public RoutingBasicItem routingBasicItem { get; set; }
        public GUIItem guiItem { get; set; }
        public UIItem uiItem { get; set; }
        public ConstItem constItem { get; set; }
        public SpeedTestItem speedTestItem { get; set; }
        public List<InItem> inbound { get; set; }
        public List<KeyEventItem> globalHotkeys { get; set; }
        public List<CoreTypeItem> coreTypeItem { get; set; }

        #endregion

    }
}
