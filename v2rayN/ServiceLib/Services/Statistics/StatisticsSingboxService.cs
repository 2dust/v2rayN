using System.Net.WebSockets;

namespace ServiceLib.Services.Statistics;

public class StatisticsSingboxService
{
    private readonly Config _config;
    private CancellationTokenSource? _cts;
    private readonly Func<ServerSpeedItem, Task>? _updateFunc;
    private string Url => $"ws://{Global.Loopback}:{AppManager.Instance.StatePort2}/traffic";
    private static readonly string _tag = "StatisticsSingboxService";

    public StatisticsSingboxService(Config config, Func<ServerSpeedItem, Task> updateFunc)
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

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!AppManager.Instance.IsRunningCore(ECoreType.sing_box))
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                    continue;
                }
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(Url), token).ConfigureAwait(false);

                var buffer = new byte[1024];
                while (ws.State == WebSocketState.Open
                    && !token.IsCancellationRequested)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    using var ms = new MemoryStream();
                    ms.Write(buffer, 0, res.Count);
                    while (!res.EndOfMessage)
                    {
                        res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                        ms.Write(buffer, 0, res.Count);
                    }

                    var result = Encoding.UTF8.GetString(ms.ToArray());
                    if (!result.IsNotEmpty())
                    {
                        continue;
                    }
                    ParseOutput(result, out var up, out var down);

                    if (_updateFunc != null)
                    {
                        await _updateFunc.Invoke(new ServerSpeedItem
                        {
                            ProxyUp = (long)(up / 1000),
                            ProxyDown = (long)(down / 1000),
                        }).ConfigureAwait(false);
                    }
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

    private void ParseOutput(string source, out ulong up, out ulong down)
    {
        up = 0;
        down = 0;
        try
        {
            var trafficItem = JsonUtils.Deserialize<TrafficItem>(source);
            if (trafficItem != null)
            {
                up = trafficItem.Up;
                down = trafficItem.Down;
            }
        }
        catch
        {
        }
    }
}
