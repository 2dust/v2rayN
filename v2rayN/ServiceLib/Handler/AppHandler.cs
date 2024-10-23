namespace ServiceLib.Handler
{
    public sealed class AppHandler
    {
        #region Property

        private static readonly Lazy<AppHandler> _instance = new(() => new());
        private Config _config;
        private int? _statePort;
        private int? _statePort2;
        private Job? _processJob;
        private bool? _isAdministrator;
        public static AppHandler Instance => _instance.Value;
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

        public bool IsAdministrator
        {
            get
            {
                _isAdministrator ??= Utils.IsAdministrator();
                return _isAdministrator.Value;
            }
        }

        #endregion Property

        #region Init

        public bool InitApp()
        {
            _config = ConfigHandler.LoadConfig();
            if (_config == null)
            {
                return false;
            }
            Thread.CurrentThread.CurrentUICulture = new(_config.UiItem.CurrentLanguage);

            //Under Win10
            if (Utils.IsWindows() && Environment.OSVersion.Version.Major < 10)
            {
                Environment.SetEnvironmentVariable("DOTNET_EnableWriteXorExecute", "0", EnvironmentVariableTarget.User);
            }

            SQLiteHelper.Instance.CreateTable<SubItem>();
            SQLiteHelper.Instance.CreateTable<ProfileItem>();
            SQLiteHelper.Instance.CreateTable<ServerStatItem>();
            SQLiteHelper.Instance.CreateTable<RoutingItem>();
            SQLiteHelper.Instance.CreateTable<ProfileExItem>();
            SQLiteHelper.Instance.CreateTable<DNSItem>();
            return true;
        }

        public bool InitComponents()
        {
            Logging.Setup();
            Logging.LoggingEnabled(true);
            Logging.SaveLog($"v2rayN start up | {Utils.GetVersion()} | {Utils.GetExePath()}");
            Logging.SaveLog($"{Environment.OSVersion} - {(Environment.Is64BitOperatingSystem ? 64 : 32)}");
            Logging.ClearLogs();

            return true;
        }

        #endregion Init

        #region Config

        public int GetLocalPort(EInboundProtocol protocol)
        {
            var localPort = _config.Inbound.FirstOrDefault(t => t.Protocol == nameof(EInboundProtocol.socks))?.LocalPort ?? 10808;
            return localPort + (int)protocol;
        }

        public void AddProcess(IntPtr processHandle)
        {
            if (Utils.IsWindows())
            {
                _processJob ??= new();
                _processJob?.AddProcess(processHandle);
            }
        }

        #endregion Config

        #region SqliteHelper

        public async Task<List<SubItem>> SubItems()
        {
            return await SQLiteHelper.Instance.TableAsync<SubItem>().OrderBy(t => t.sort).ToListAsync();
        }

        public async Task<SubItem> GetSubItem(string subid)
        {
            return await SQLiteHelper.Instance.TableAsync<SubItem>().FirstOrDefaultAsync(t => t.id == subid);
        }

        public async Task<List<ProfileItem>> ProfileItems(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return await SQLiteHelper.Instance.TableAsync<ProfileItem>().ToListAsync();
            }
            else
            {
                return await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.subid == subid).ToListAsync();
            }
        }

        public async Task<List<string>> ProfileItemIndexes(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return (await SQLiteHelper.Instance.TableAsync<ProfileItem>().ToListAsync())
                        .Select(t => t.indexId)
                        .ToList();
            }
            else
            {
                return (await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(t => t.subid == subid).ToListAsync())
                        .Select(t => t.indexId)
                        .ToList();
            }
        }

        public async Task<List<ProfileItemModel>> ProfileItems(string subid, string filter)
        {
            var sql = @$"select a.*
                           ,b.remarks subRemarks
                        from ProfileItem a
                        left join SubItem b on a.subid = b.id
                        where 1=1 ";
            if (Utils.IsNotEmpty(subid))
            {
                sql += $" and a.subid = '{subid}'";
            }
            if (Utils.IsNotEmpty(filter))
            {
                if (filter.Contains('\''))
                {
                    filter = filter.Replace("'", "");
                }
                sql += string.Format(" and (a.remarks like '%{0}%' or a.address like '%{0}%') ", filter);
            }

            return await SQLiteHelper.Instance.QueryAsync<ProfileItemModel>(sql);
        }

        public async Task<List<ProfileItemModel>> ProfileItemsEx(string subid, string filter)
        {
            var lstModel = await ProfileItems(_config.SubIndexId, filter);

            await ConfigHandler.SetDefaultServer(_config, lstModel);

            var lstServerStat = (_config.GuiItem.EnableStatistics ? StatisticsHandler.Instance.ServerStat : null) ?? [];
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
                            isActive = t.indexId == _config.IndexId,
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

        public async Task<ProfileItem?> GetProfileItem(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return null;
            }
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(it => it.indexId == indexId);
        }

        public async Task<ProfileItem?> GetProfileItemViaRemarks(string? remarks)
        {
            if (Utils.IsNullOrEmpty(remarks))
            {
                return null;
            }
            return await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync(it => it.remarks == remarks);
        }

        public async Task<List<RoutingItem>> RoutingItems()
        {
            return await SQLiteHelper.Instance.TableAsync<RoutingItem>().Where(it => it.locked == false).OrderBy(t => t.sort).ToListAsync();
        }

        public async Task<RoutingItem> GetRoutingItem(string id)
        {
            return await SQLiteHelper.Instance.TableAsync<RoutingItem>().FirstOrDefaultAsync(it => it.locked == false && it.id == id);
        }

        public async Task<List<DNSItem>> DNSItems()
        {
            return await SQLiteHelper.Instance.TableAsync<DNSItem>().ToListAsync();
        }

        public async Task<DNSItem> GetDNSItem(ECoreType eCoreType)
        {
            return await SQLiteHelper.Instance.TableAsync<DNSItem>().FirstOrDefaultAsync(it => it.coreType == eCoreType);
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

            if (_config.CoreTypeItem == null)
            {
                return ECoreType.Xray;
            }
            var item = _config.CoreTypeItem.FirstOrDefault(it => it.ConfigType == eConfigType);
            if (item == null)
            {
                return ECoreType.Xray;
            }
            return item.CoreType;
        }

        #endregion Core Type
    }
}