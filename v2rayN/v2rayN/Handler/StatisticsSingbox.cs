using System.Net.WebSockets;
using System.Text;
using v2rayN.Models;

namespace v2rayN.Handler
{
    internal class StatisticsSingbox
    {
        private Config _config;
        private bool _exitFlag;
        private ClientWebSocket? webSocket;
        private string url = string.Empty;
        private Action<ServerSpeedItem> _updateFunc;

        public StatisticsSingbox(Config config, Action<ServerSpeedItem> update)
        {
            _config = config;
            _updateFunc = update;
            _exitFlag = false;

            Task.Run(() => Run());
        }

        private async void Init()
        {
            await Task.Delay(5000);

            try
            {
                url = $"ws://{Global.Loopback}:{LazyConfig.Instance.StatePort}/traffic";

                if (webSocket == null)
                {
                    webSocket = new ClientWebSocket();
                    await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
                }
            }
            catch { }
        }

        public void Close()
        {
            try
            {
                _exitFlag = true;
                if (webSocket != null)
                {
                    webSocket.Abort();
                    webSocket = null;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private async void Run()
        {
            Init();

            while (!_exitFlag)
            {
                await Task.Delay(1000);
                try
                {
                    if (!(_config.runningCoreType is ECoreType.sing_box or ECoreType.clash or ECoreType.clash_meta or ECoreType.mihomo))
                    {
                        continue;
                    }
                    if (webSocket != null)
                    {
                        if (webSocket.State == WebSocketState.Aborted
                            || webSocket.State == WebSocketState.Closed)
                        {
                            webSocket.Abort();
                            webSocket = null;
                            Init();
                            continue;
                        }

                        if (webSocket.State != WebSocketState.Open)
                        {
                            continue;
                        }

                        var buffer = new byte[1024];
                        var res = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        while (!res.CloseStatus.HasValue)
                        {
                            var result = Encoding.UTF8.GetString(buffer, 0, res.Count);
                            if (!Utils.IsNullOrEmpty(result))
                            {
                                ParseOutput(result, out ulong up, out ulong down);

                                _updateFunc(new ServerSpeedItem()
                                {
                                    proxyUp = (long)(up / 1000),
                                    proxyDown = (long)(down / 1000)
                                });
                            }
                            res = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void ParseOutput(string source, out ulong up, out ulong down)
        {
            up = 0; down = 0;
            try
            {
                var trafficItem = JsonUtils.Deserialize<TrafficItem>(source);
                if (trafficItem != null)
                {
                    up = trafficItem.up;
                    down = trafficItem.down;
                }
            }
            catch
            {
            }
        }
    }
}