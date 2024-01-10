using System.Net;
using System.Net.Sockets;
using v2rayN.Mode;

namespace v2rayN.Handler
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
            Global.StatePort = GetFreePort();

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
            SqliteHelper.Instance.Execute($"delete from ServerStatItem ");
            _serverStatItem = null;
            _lstServerStat = new();
        }

        public void SaveTo()
        {
            try
            {
                SqliteHelper.Instance.UpdateAll(_lstServerStat);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private void Init()
        {
            SqliteHelper.Instance.Execute($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

            long ticks = DateTime.Now.Date.Ticks;
            SqliteHelper.Instance.Execute($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

            _lstServerStat = SqliteHelper.Instance.Table<ServerStatItem>().ToList();
        }

        private void UpdateServerStat(ServerSpeedItem server)
        {
            GetServerStatItem(_config.indexId);

            if (server.proxyUp != 0 || server.proxyDown != 0)
            {
                _serverStatItem.todayUp += server.proxyUp;
                _serverStatItem.todayDown += server.proxyDown;
                _serverStatItem.totalUp += server.proxyUp;
                _serverStatItem.totalDown += server.proxyDown;
            }
            if (Global.ShowInTaskbar)
            {
                server.indexId = _config.indexId;
                server.todayUp = _serverStatItem.todayUp;
                server.todayDown = _serverStatItem.todayDown;
                server.totalUp = _serverStatItem.totalUp;
                server.totalDown = _serverStatItem.totalDown;
                _updateFunc(server);
            }
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
                    SqliteHelper.Instance.Replace(_serverStatItem);
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

        private int GetFreePort()
        {
            try
            {
                int defaultPort = 9090;
                if (!Utils.PortInUse(defaultPort))
                {
                    return defaultPort;
                }

                TcpListener l = new(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch
            {
            }
            return 69090;
        }
    }
}