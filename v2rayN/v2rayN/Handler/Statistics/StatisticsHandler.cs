using v2rayN.Models;

namespace v2rayN.Handler.Statistics
{
    internal class StatisticsHandler
    {
        private Config _config;
        private ServerStatItem? _serverStatItem;
        private List<ServerStatItem> _lstServerStat;
        private Action<ServerSpeedItem> _updateFunc;
        private StatisticsV2ray? _statisticsV2Ray;
        private StatisticsSingbox? _statisticsSingbox;

        public List<ServerStatItem> ServerStat => _lstServerStat;
        public bool Enable { get; set; }

        public StatisticsHandler(Config config, Action<ServerSpeedItem> update)
        {
            _config = config;
            Enable = config.guiItem.enableStatistics;
            if (!Enable)
            {
                return;
            }

            _updateFunc = update;

            Init();

            _statisticsV2Ray = new StatisticsV2ray(config, UpdateServerStat);
            _statisticsSingbox = new StatisticsSingbox(config, UpdateServerStat);
        }

        public void Close()
        {
            try
            {
                _statisticsV2Ray?.Close();
                _statisticsSingbox?.Close();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public void ClearAllServerStatistics()
        {
            SQLiteHelper.Instance.Execute($"delete from ServerStatItem ");
            _serverStatItem = null;
            _lstServerStat = new();
        }

        public void SaveTo()
        {
            try
            {
                SQLiteHelper.Instance.UpdateAll(_lstServerStat);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private void Init()
        {
            SQLiteHelper.Instance.Execute($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

            long ticks = DateTime.Now.Date.Ticks;
            SQLiteHelper.Instance.Execute($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

            _lstServerStat = SQLiteHelper.Instance.Table<ServerStatItem>().ToList();
        }

        private void UpdateServerStat(ServerSpeedItem server)
        {
            GetServerStatItem(_config.indexId);

            if (_serverStatItem is null)
            {
                return;
            }
            if (server.proxyUp != 0 || server.proxyDown != 0)
            {
                _serverStatItem.todayUp += server.proxyUp;
                _serverStatItem.todayDown += server.proxyDown;
                _serverStatItem.totalUp += server.proxyUp;
                _serverStatItem.totalDown += server.proxyDown;
            }

            server.indexId = _config.indexId;
            server.todayUp = _serverStatItem.todayUp;
            server.todayDown = _serverStatItem.todayDown;
            server.totalUp = _serverStatItem.totalUp;
            server.totalDown = _serverStatItem.totalDown;
            _updateFunc(server);
        }

        private void GetServerStatItem(string indexId)
        {
            long ticks = DateTime.Now.Date.Ticks;
            if (_serverStatItem != null && _serverStatItem.indexId != indexId)
            {
                _serverStatItem = null;
            }

            if (_serverStatItem == null)
            {
                _serverStatItem = _lstServerStat.FirstOrDefault(t => t.indexId == indexId);
                if (_serverStatItem == null)
                {
                    _serverStatItem = new ServerStatItem
                    {
                        indexId = indexId,
                        totalUp = 0,
                        totalDown = 0,
                        todayUp = 0,
                        todayDown = 0,
                        dateNow = ticks
                    };
                    SQLiteHelper.Instance.Replace(_serverStatItem);
                    _lstServerStat.Add(_serverStatItem);
                }
            }

            if (_serverStatItem.dateNow != ticks)
            {
                _serverStatItem.todayUp = 0;
                _serverStatItem.todayDown = 0;
                _serverStatItem.dateNow = ticks;
            }
        }
    }
}