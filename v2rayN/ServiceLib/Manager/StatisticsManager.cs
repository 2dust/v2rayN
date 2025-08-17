namespace ServiceLib.Manager;

public class StatisticsManager
{
    private static readonly Lazy<StatisticsManager> instance = new(() => new());
    public static StatisticsManager Instance => instance.Value;

    private Config _config;
    private ServerStatItem? _serverStatItem;
    private List<ServerStatItem> _lstServerStat;
    private Action<ServerSpeedItem>? _updateFunc;

    private StatisticsXrayService? _statisticsXray;
    private StatisticsSingboxService? _statisticsSingbox;
    private static readonly string _tag = "StatisticsHandler";
    public List<ServerStatItem> ServerStat => _lstServerStat;

    public async Task Init(Config config, Action<ServerSpeedItem> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;
        if (config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            await InitData();

            _statisticsXray = new StatisticsXrayService(config, UpdateServerStatHandler);
            _statisticsSingbox = new StatisticsSingboxService(config, UpdateServerStatHandler);
        }
    }

    public void Close()
    {
        try
        {
            _statisticsXray?.Close();
            _statisticsSingbox?.Close();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
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
            Logging.SaveLog(_tag, ex);
        }
    }

    public async Task CloneServerStatItem(string indexId, string toIndexId)
    {
        if (_lstServerStat == null)
        {
            return;
        }

        if (indexId == toIndexId)
        {
            return;
        }

        var stat = _lstServerStat.FirstOrDefault(t => t.IndexId == indexId);
        if (stat == null)
        {
            return;
        }

        var toStat = JsonUtils.DeepCopy(stat);
        toStat.IndexId = toIndexId;
        await SQLiteHelper.Instance.ReplaceAsync(toStat);
        _lstServerStat.Add(toStat);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

        var ticks = DateTime.Now.Date.Ticks;
        await SQLiteHelper.Instance.ExecuteAsync($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

        _lstServerStat = await SQLiteHelper.Instance.TableAsync<ServerStatItem>().ToListAsync();
    }

    private void UpdateServerStatHandler(ServerSpeedItem server)
    {
        _ = UpdateServerStat(server);
    }

    private async Task UpdateServerStat(ServerSpeedItem server)
    {
        await GetServerStatItem(_config.IndexId);

        if (_serverStatItem is null)
        {
            return;
        }
        if (server.ProxyUp != 0 || server.ProxyDown != 0)
        {
            _serverStatItem.TodayUp += server.ProxyUp;
            _serverStatItem.TodayDown += server.ProxyDown;
            _serverStatItem.TotalUp += server.ProxyUp;
            _serverStatItem.TotalDown += server.ProxyDown;
        }

        server.IndexId = _config.IndexId;
        server.TodayUp = _serverStatItem.TodayUp;
        server.TodayDown = _serverStatItem.TodayDown;
        server.TotalUp = _serverStatItem.TotalUp;
        server.TotalDown = _serverStatItem.TotalDown;
        _updateFunc?.Invoke(server);
    }

    private async Task GetServerStatItem(string indexId)
    {
        var ticks = DateTime.Now.Date.Ticks;
        if (_serverStatItem != null && _serverStatItem.IndexId != indexId)
        {
            _serverStatItem = null;
        }

        if (_serverStatItem == null)
        {
            _serverStatItem = _lstServerStat.FirstOrDefault(t => t.IndexId == indexId);
            if (_serverStatItem == null)
            {
                _serverStatItem = new ServerStatItem
                {
                    IndexId = indexId,
                    TotalUp = 0,
                    TotalDown = 0,
                    TodayUp = 0,
                    TodayDown = 0,
                    DateNow = ticks
                };
                await SQLiteHelper.Instance.ReplaceAsync(_serverStatItem);
                _lstServerStat.Add(_serverStatItem);
            }
        }

        if (_serverStatItem.DateNow != ticks)
        {
            _serverStatItem.TodayUp = 0;
            _serverStatItem.TodayDown = 0;
            _serverStatItem.DateNow = ticks;
        }
    }
}
