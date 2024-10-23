namespace ServiceLib.Models
{
    /// <summary>
    /// 本软件配置文件实体类
    /// </summary>
    [Serializable]
    public class Config
    {
        #region property

        public string IndexId { get; set; }
        public string SubIndexId { get; set; }

        public ECoreType RunningCoreType { get; set; }

        public bool IsRunningCore(ECoreType type)
        {
            if (type == ECoreType.Xray && RunningCoreType is ECoreType.Xray or ECoreType.v2fly or ECoreType.v2fly_v5)
            {
                return true;
            }
            if (type == ECoreType.sing_box && RunningCoreType is ECoreType.sing_box or ECoreType.mihomo)
            {
                return true;
            }
            return false;
        }

        #endregion property

        #region other entities

        public CoreBasicItem CoreBasicItem { get; set; }
        public TunModeItem TunModeItem { get; set; }
        public KcpItem KcpItem { get; set; }
        public GrpcItem GrpcItem { get; set; }
        public RoutingBasicItem RoutingBasicItem { get; set; }
        public GUIItem GuiItem { get; set; }
        public MsgUIItem MsgUIItem { get; set; }
        public UIItem UiItem { get; set; }
        public ConstItem ConstItem { get; set; }
        public SpeedTestItem SpeedTestItem { get; set; }
        public Mux4RayItem Mux4RayItem { get; set; }
        public Mux4SboxItem Mux4SboxItem { get; set; }
        public HysteriaItem HysteriaItem { get; set; }
        public ClashUIItem ClashUIItem { get; set; }
        public SystemProxyItem SystemProxyItem { get; set; }
        public WebDavItem WebDavItem { get; set; }
        public List<InItem> Inbound { get; set; }
        public List<KeyEventItem> GlobalHotkeys { get; set; }
        public List<CoreTypeItem> CoreTypeItem { get; set; }

        #endregion other entities
    }
}