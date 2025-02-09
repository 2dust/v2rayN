namespace ServiceLib.Services.Statistics
{
    public class StatisticsXrayService
    {
        private const long linkBase = 1024;
        private ServerSpeedItem _serverSpeedItem = new();
        private Config _config;
        private bool _exitFlag;
        private Action<ServerSpeedItem>? _updateFunc;
        private string Url => $"{Global.HttpProtocol}{Global.Loopback}:{AppHandler.Instance.StatePort}/debug/vars";

        public StatisticsXrayService(Config config, Action<ServerSpeedItem> updateFunc)
        {
            _config = config;
            _updateFunc = updateFunc;
            _exitFlag = false;

            Task.Run(Run);
        }

        public void Close()
        {
            _exitFlag = true;
        }

        private async Task Run()
        {
            while (!_exitFlag)
            {
                await Task.Delay(1000);
                try
                {
                    if (_config.RunningCoreType != ECoreType.Xray)
                    {
                        continue;
                    }

                    var result = await HttpClientHelper.Instance.TryGetAsync(Url);
                    if (result != null)
                    {
                        var server = ParseOutput(result) ?? new ServerSpeedItem();
                        _updateFunc?.Invoke(server);
                    }
                }
                catch
                {
                    // ignored
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
                foreach (string key in source.stats.outbound.Keys)
                {
                    var value = source.stats.outbound[key];
                    if (value == null)
                        continue;
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
}
