namespace ServiceLib.Models
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

        public ECoreType runningCoreType { get; set; }

        public bool IsRunningCore(ECoreType type)
        {
            if (type == ECoreType.Xray && runningCoreType is ECoreType.Xray or ECoreType.v2fly or ECoreType.v2fly_v5)
            {
                return true;
            }
            if (type == ECoreType.sing_box && runningCoreType is ECoreType.sing_box or ECoreType.mihomo)
            {
                return true;
            }
            return false;
        }

        #endregion property

        #region other entities

        public CoreBasicItem coreBasicItem { get; set; }
        public TunModeItem tunModeItem { get; set; }
        public KcpItem kcpItem { get; set; }
        public GrpcItem grpcItem { get; set; }
        public RoutingBasicItem routingBasicItem { get; set; }
        public GUIItem guiItem { get; set; }
        public MsgUIItem msgUIItem { get; set; }
        public UIItem uiItem { get; set; }
        public ConstItem constItem { get; set; }
        public SpeedTestItem speedTestItem { get; set; }
        public Mux4RayItem mux4RayItem { get; set; }
        public Mux4SboxItem mux4SboxItem { get; set; }
        public HysteriaItem hysteriaItem { get; set; }
        public ClashUIItem clashUIItem { get; set; }
        public SystemProxyItem systemProxyItem { get; set; }
        public WebDavItem webDavItem { get; set; }
        public List<InItem> inbound { get; set; }
        public List<KeyEventItem> globalHotkeys { get; set; }
        public List<CoreTypeItem> coreTypeItem { get; set; }

        #endregion other entities
    }
}