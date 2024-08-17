using v2rayN.Enums;
using v2rayN.Handler.Statistics;
using v2rayN.Models;

namespace v2rayN.Handler
{
    public sealed class LazyConfig
    {
        private static readonly Lazy<LazyConfig> _instance = new(() => new());
        private Config _config;
        private int? _statePort;
        private int? _statePort2;

        public static LazyConfig Instance => _instance.Value;
        public Config Config => _config;

        public int StatePort
        {
            get
            {
                _statePort ??= Utils.GetFreePort(GetLocalPort(EInboundProtocol.api));
                return _statePort.Value;
            }
        }

        public int StatePort2
        {
            get
            {
                _statePort2 ??= Utils.GetFreePort(GetLocalPort(EInboundProtocol.api2));
                return _statePort2.Value;
            }
        }

        private Job _processJob = new();

        public LazyConfig()
        {
            SQLiteHelper.Instance.CreateTable<SubItem>();
            SQLiteHelper.Instance.CreateTable<ProfileItem>();
            SQLiteHelper.Instance.CreateTable<ServerStatItem>();
            SQLiteHelper.Instance.CreateTable<RoutingItem>();
            SQLiteHelper.Instance.CreateTable<ProfileExItem>();
            SQLiteHelper.Instance.CreateTable<DNSItem>();
        }

        #region Config

        public void SetConfig(Config config) => _config = config;

        public int GetLocalPort(EInboundProtocol protocol)
        {
            var localPort = _config.inbound.FirstOrDefault(t => t.protocol == nameof(EInboundProtocol.socks))?.localPort ?? 10808;
            return localPort + (int)protocol;
        }

        public void AddProcess(IntPtr processHandle)
        {
            _processJob.AddProcess(processHandle);
        }

        #endregion Config

        #region SqliteHelper

        public List<SubItem> SubItems()
        {
            return SQLiteHelper.Instance.Table<SubItem>().ToList();
        }

        public SubItem GetSubItem(string subid)
        {
            return SQLiteHelper.Instance.Table<SubItem>().FirstOrDefault(t => t.id == subid);
        }

        public List<ProfileItem> ProfileItems(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().ToList();
            }
            else
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid).ToList();
            }
        }

        public List<string> ProfileItemIndexes(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().Select(t => t.indexId).ToList();
            }
            else
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid).Select(t => t.indexId).ToList();
            }
        }

        public List<ProfileItemModel> ProfileItems(string subid, string filter)
        {
            var sql = @$"select a.*
                           ,b.remarks subRemarks
                        from ProfileItem a
                        left join SubItem b on a.subid = b.id
                        where 1=1 ";
            if (!Utils.IsNullOrEmpty(subid))
            {
                sql += $" and a.subid = '{subid}'";
            }
            if (!Utils.IsNullOrEmpty(filter))
            {
                if (filter.Contains('\''))
                {
                    filter = filter.Replace("'", "");
                }
                sql += String.Format(" and (a.remarks like '%{0}%' or a.address like '%{0}%') ", filter);
            }

            return SQLiteHelper.Instance.Query<ProfileItemModel>(sql).ToList();
        }

        public List<ProfileItemModel> ProfileItemsEx(string subid, string filter)
        {
            var lstModel = ProfileItems(_config.subIndexId, filter);

            ConfigHandler.SetDefaultServer(_config, lstModel);

            var lstServerStat = (_config.guiItem.enableStatistics ? StatisticsHandler.Instance.ServerStat : null) ?? [];
            var lstProfileExs = ProfileExHandler.Instance.ProfileExs;
            lstModel = (from t in lstModel
                        join t2 in lstServerStat on t.indexId equals t2.indexId into t2b
                        from t22 in t2b.DefaultIfEmpty()
                        join t3 in lstProfileExs on t.indexId equals t3.indexId into t3b
                        from t33 in t3b.DefaultIfEmpty()
                        select new ProfileItemModel
                        {
                            indexId = t.indexId,
                            configType = t.configType,
                            remarks = t.remarks,
                            address = t.address,
                            port = t.port,
                            security = t.security,
                            network = t.network,
                            streamSecurity = t.streamSecurity,
                            subid = t.subid,
                            subRemarks = t.subRemarks,
                            isActive = t.indexId == _config.indexId,
                            sort = t33 == null ? 0 : t33.sort,
                            delay = t33 == null ? 0 : t33.delay,
                            delayVal = t33?.delay != 0 ? $"{t33?.delay} {Global.DelayUnit}" : string.Empty,
                            speedVal = t33?.speed != 0 ? $"{t33?.speed} {Global.SpeedUnit}" : string.Empty,
                            todayDown = t22 == null ? "" : Utils.HumanFy(t22.todayDown),
                            todayUp = t22 == null ? "" : Utils.HumanFy(t22.todayUp),
                            totalDown = t22 == null ? "" : Utils.HumanFy(t22.totalDown),
                            totalUp = t22 == null ? "" : Utils.HumanFy(t22.totalUp)
                        }).OrderBy(t => t.sort).ToList();

            return lstModel;
        }

        public ProfileItem? GetProfileItem(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return null;
            }
            return SQLiteHelper.Instance.Table<ProfileItem>().FirstOrDefault(it => it.indexId == indexId);
        }

        public ProfileItem? GetProfileItemViaRemarks(string remarks)
        {
            if (Utils.IsNullOrEmpty(remarks))
            {
                return null;
            }
            return SQLiteHelper.Instance.Table<ProfileItem>().FirstOrDefault(it => it.remarks == remarks);
        }

        public List<RoutingItem> RoutingItems()
        {
            return SQLiteHelper.Instance.Table<RoutingItem>().Where(it => it.locked == false).OrderBy(t => t.sort).ToList();
        }

        public RoutingItem GetRoutingItem(string id)
        {
            return SQLiteHelper.Instance.Table<RoutingItem>().FirstOrDefault(it => it.locked == false && it.id == id);
        }

        public List<DNSItem> DNSItems()
        {
            return SQLiteHelper.Instance.Table<DNSItem>().ToList();
        }

        public DNSItem GetDNSItem(ECoreType eCoreType)
        {
            return SQLiteHelper.Instance.Table<DNSItem>().FirstOrDefault(it => it.coreType == eCoreType);
        }

        #endregion SqliteHelper

        #region Core Type

        public List<string> GetShadowsocksSecurities(ProfileItem profileItem)
        {
            var coreType = GetCoreType(profileItem, EConfigType.Shadowsocks);
            switch (coreType)
            {
                case ECoreType.v2fly:
                    return Global.SsSecurities;

                case ECoreType.Xray:
                    return Global.SsSecuritiesInXray;

                case ECoreType.sing_box:
                    return Global.SsSecuritiesInSingbox;
            }
            return Global.SsSecuritiesInSagerNet;
        }

        public ECoreType GetCoreType(ProfileItem profileItem, EConfigType eConfigType)
        {
            if (profileItem?.coreType != null)
            {
                return (ECoreType)profileItem.coreType;
            }

            if (_config.coreTypeItem == null)
            {
                return ECoreType.Xray;
            }
            var item = _config.coreTypeItem.FirstOrDefault(it => it.configType == eConfigType);
            if (item == null)
            {
                return ECoreType.Xray;
            }
            return item.coreType;
        }

        #endregion Core Type
    }
}