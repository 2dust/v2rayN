namespace ServiceLib.Handler
{
    public class StatisticsHandler
    {
        private static readonly Lazy<StatisticsHandler> instance = new(() => new());
        public static StatisticsHandler Instance => instance.Value;

        private Config _config;
        private ServerStatItem? _serverStatItem;
        private List<ServerStatItem> _lstServerStat;
        private Action<ServerSpeedItem>? _updateFunc;
        private StatisticsV2rayService? _statisticsV2Ray;
        private StatisticsSingboxService? _statisticsSingbox;

        public List<ServerStatItem> ServerStat => _lstServerStat;

        public async Task Init(Config config, Action<ServerSpeedItem> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;
            if (!config.guiItem.enableStatistics)
            {
                return;
            }

            await InitData();

            _statisticsV2Ray = new StatisticsV2rayService(config, UpdateServerStatHandler);
            _statisticsSingbox = new StatisticsSingboxService(config, UpdateServerStatHandler);
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

        public async Task ClearAllServerStatistics()
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ServerStatItem ");
            _serverStatItem = null;
            _lstServerStat = new();
        }

        public async Task SaveTo()
        {
            try
            {
                if (_lstServerStat != null)
                {
                    await SQLiteHelper.Instance.UpdateAllAsync(_lstServerStat);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private async Task InitData()
        {
            await SQLiteHelper.Instance.ExecuteAsync($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

            long ticks = DateTime.Now.Date.Ticks;
            await SQLiteHelper.Instance.ExecuteAsync($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

            _lstServerStat = await SQLiteHelper.Instance.TableAsync<ServerStatItem>().ToListAsync();
        }

        private void UpdateServerStatHandler(ServerSpeedItem server)
        {
            UpdateServerStat(server);
        }

        private async Task UpdateServerStat(ServerSpeedItem server)
        {
            await GetServerStatItem(_config.indexId);

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
            _updateFunc?.Invoke(server);
        }

        private async Task GetServerStatItem(string indexId)
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
                    await SQLiteHelper.Instance.ReplaceAsync(_serverStatItem);
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