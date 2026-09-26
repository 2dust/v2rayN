namespace ServiceLib.Services.Statistics;

public class StatisticsXrayService
{
    private const long linkBase = 1024;
    private ServerSpeedItem _serverSpeedItem = new();
    private readonly Config _config;
    private CancellationTokenSource? _cts;
    private readonly Func<ServerSpeedItem, Task>? _updateFunc;
    private string Url => $"{Global.HttpProtocol}{Global.Loopback}:{AppManager.Instance.StatePort}/debug/vars";
    private static readonly string _tag = "StatisticsXrayService";

    public StatisticsXrayService(Config config, Func<ServerSpeedItem, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        Task.Run(Run);
    }

    public void Close()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private async Task Run()
    {
        Close();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            try
            {
                if (AppManager.Instance.RunningCoreType != ECoreType.Xray)
                {
                    continue;
                }

                var result = await HttpClientHelper.Instance.TryGetAsync(Url, token);
                if (result != null)
                {
                    var server = ParseOutput(result) ?? new ServerSpeedItem();
                    await _updateFunc!.Invoke(server);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(3000, token).ConfigureAwait(false);
            }
        }
    }

    private ServerSpeedItem? ParseOutput(string result)
    {
        try
        {
            var source = JsonUtils.Deserialize<V2rayMetricsVars>(result);
            if (source?.stats?.outbound == null)
            {
                return null;
            }

            ServerSpeedItem server = new();
            foreach (var key in source.stats.outbound.Keys.Cast<string>())
            {
                var value = source.stats.outbound[key];
                if (value == null)
                {
                    continue;
                }
                var state = JsonUtils.Deserialize<V2rayMetricsVarsLink>(value.ToString());

                if (key.StartsWith(Global.ProxyTag))
                {
                    server.ProxyUp += state.uplink / linkBase;
                    server.ProxyDown += state.downlink / linkBase;
                }
                else if (key == Global.DirectTag)
                {
                    server.DirectUp = state.uplink / linkBase;
                    server.DirectDown = state.downlink / linkBase;
                }
            }

            if (server.DirectDown < _serverSpeedItem.DirectDown || server.ProxyDown < _serverSpeedItem.ProxyDown)
            {
                _serverSpeedItem = new();
                return null;
            }

            ServerSpeedItem curItem = new()
            {
                ProxyUp = server.ProxyUp - _serverSpeedItem.ProxyUp,
                ProxyDown = server.ProxyDown - _serverSpeedItem.ProxyDown,
                DirectUp = server.DirectUp - _serverSpeedItem.DirectUp,
                DirectDown = server.DirectDown - _serverSpeedItem.DirectDown,
            };
            _serverSpeedItem = server;
            return curItem;
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
