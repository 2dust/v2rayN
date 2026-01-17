namespace ServiceLib.Manager;

public class StatisticsManager
{
    private static readonly Lazy<StatisticsManager> instance = new(() => new());
    public static StatisticsManager Instance => instance.Value;

    private Config _config;
    private ServerStatItem? _serverStatItem;
    private Func<ServerSpeedItem, Task>? _updateFunc;

    private StatisticsXrayService? _statisticsXray;
    private StatisticsSingboxService? _statisticsSingbox;
    private static readonly string _tag = "StatisticsHandler";
    public List<ServerStatItem> ServerStat { get; private set; }

    public async Task Init(Config config, Func<ServerSpeedItem, Task> updateFunc)
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
        ServerStat = new();
    }

    public async Task SaveTo()
    {
        try
        {
            if (ServerStat is not null)
            {
                await SQLiteHelper.Instance.UpdateAllAsync(ServerStat);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public async Task CloneServerStatItem(string indexId, string toIndexId)
    {
        if (ServerStat is null)
        {
            return;
        }

        if (indexId == toIndexId)
        {
            return;
        }

        var stat = ServerStat.FirstOrDefault(t => t.IndexId == indexId);
        if (stat is null)
        {
            return;
        }

        var toStat = JsonUtils.DeepCopy(stat);
        toStat.IndexId = toIndexId;
        await SQLiteHelper.Instance.ReplaceAsync(toStat);
        ServerStat.Add(toStat);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ServerStatItem where indexId not in ( select indexId from ProfileItem )");

        var ticks = DateTime.Now.Date.Ticks;
        await SQLiteHelper.Instance.ExecuteAsync($"update ServerStatItem set todayUp = 0,todayDown=0,dateNow={ticks} where dateNow<>{ticks}");

        ServerStat = await SQLiteHelper.Instance.TableAsync<ServerStatItem>().ToListAsync();
    }

    private async Task UpdateServerStatHandler(ServerSpeedItem server)
    {
        await UpdateServerStat(server);
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
        await _updateFunc?.Invoke(server);
    }

    private async Task GetServerStatItem(string indexId)
    {
        var ticks = DateTime.Now.Date.Ticks;
        if (_serverStatItem is not null && _serverStatItem.IndexId != indexId)
        {
            _serverStatItem = null;
        }

        if (_serverStatItem is null)
        {
            _serverStatItem = ServerStat.FirstOrDefault(t => t.IndexId == indexId);
            if (_serverStatItem is null)
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
                ServerStat.Add(_serverStatItem);
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
