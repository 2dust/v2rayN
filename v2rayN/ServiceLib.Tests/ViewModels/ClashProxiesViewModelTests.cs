using ReactiveUI.Builder;
using ServiceLib.Tests.CoreConfig;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.ViewModels;

[NotInParallel]
public class ClashProxiesViewModelTests
{
    static ClashProxiesViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }

    [Test]
    [Arguments("Selector", true)]
    [Arguments("URLTest", true)]
    [Arguments("LoadBalance", true)]
    [Arguments("Fallback", true)]
    [Arguments("selector", true)]
    [Arguments("SELECTOR", true)]
    [Arguments("Direct", false)]
    [Arguments(null, false)]
    public async Task GroupDelay_ShouldTestAllowedGroupTypes(string? type, bool allowed)
    {
        var configField = typeof(AppManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var portField = typeof(AppManager).GetField("_statePort2", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var viewConfigField = typeof(MyReactiveObject).GetField("_config", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousConfig = configField.GetValue(AppManager.Instance);
        var previousPort = portField.GetValue(AppManager.Instance);
        var previousViewConfig = viewConfigField.GetValue(null);
        await using var server = new DelayServer();
        try
        {
            CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());
            portField.SetValue(AppManager.Instance, server.Port);
            var viewModel = new ClashProxiesViewModel();
            var item = new ClashItem();
            item.Proxies.Add("test-group", new() { type = type, all = ["test-node"] });
            item.Proxies.Add("test-node", new() { type = "Shadowsocks" });
            typeof(ClashProxiesViewModel).GetField("_clashItem", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewModel, item);
            viewModel.SelectedGroup = new ClashProxyModel { Name = "test-group" };

            var method = typeof(ClashProxiesViewModel).GetMethod("TestGroupProxiesDelay", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await ((Task)method.Invoke(viewModel, null)!).WaitAsync(TimeSpan.FromSeconds(10));

            await server.Requests.Count.Should().BeEqualTo(allowed ? 1 : 0);
            await viewModel.ProxyDetails.Single().DelayName.Should().BeEqualTo(allowed ? "42ms" : string.Empty);
            if (allowed)
            {
                await server.Requests.Single().StartsWith("GET /proxies/test-node/delay?").Should().BeTrue();
            }
        }
        finally
        {
            configField.SetValue(AppManager.Instance, previousConfig);
            portField.SetValue(AppManager.Instance, previousPort);
            viewConfigField.SetValue(null, previousViewConfig);
        }
    }

    private sealed class DelayServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public ConcurrentQueue<string> Requests { get; } = new();

        public DelayServer()
        {
            _listener.Start();
            _serverTask = ServeAsync();
        }

        private async Task ServeAsync()
        {
            var token = _cancellation.Token;
            while (!token.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = await reader.ReadLineAsync(token);
                while (await reader.ReadLineAsync(token) is { Length: > 0 })
                {
                }
                Requests.Enqueue(request!);
                const string body = "{\"delay\":42}";
                var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Stop();
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _cancellation.Dispose();
            }
        }
    }
}
