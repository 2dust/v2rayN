using ServiceLib.Services.AppRouting;

namespace ServiceLib.Tests.AppRouting;

public class ShutdownTests
{
    [Test]
    [Arguments(SocketError.ConnectionReset)]
    [Arguments(SocketError.ConnectionAborted)]
    public async Task AbortedAcceptDoesNotDisableTheListener(SocketError error)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var errors = new ConcurrentQueue<string>();
        var engine = new AppRouteEngine([], [], errors.Enqueue);
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        async ValueTask<Socket> Next(CancellationToken token)
        {
            if (attempts++ == 0)
            {
                throw new SocketException((int)error);
            }
            retried.SetResult();
            return await listener.AcceptAsync(token);
        }
        var loop = engine.Accept(Next);
        Workers(engine).Add(loop);
        try
        {
            var first = await Task.WhenAny(loop, retried.Task).WaitAsync(timeout.Token);
            await (first == retried.Task).Should().BeTrue();
            await errors.IsEmpty.Should().BeTrue();
        }
        finally { await engine.DisposeAsync().AsTask().WaitAsync(timeout.Token); }
    }

    [Test]
    public async Task FatalListenerFailureStopsOtherWorkersAndClosesListeners()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var errors = new ConcurrentQueue<string>();
        var engine = new AppRouteEngine([], [], errors.Enqueue);
        var listeners = (List<Socket>)typeof(AppRouteEngine).GetField("_listeners", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        listeners.Add(listener);
        var other = engine.Accept(listener.AcceptAsync);
        Workers(engine).Add(other);
        try
        {
            await engine.Accept(_ => ValueTask.FromException<Socket>(new SocketException((int)SocketError.NetworkDown)));
            await listener.SafeHandle.IsClosed.Should().BeTrue();
            await other.WaitAsync(timeout.Token);
            await errors.Count.Should().BeEqualTo(1);
            await errors.Single().StartsWith("Application routing stopped:").Should().BeTrue();
        }
        finally { await engine.DisposeAsync().AsTask().WaitAsync(timeout.Token); }
    }

    private static List<Task> Workers(AppRouteEngine engine) =>
        (List<Task>)typeof(AppRouteEngine).GetField("_workers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    public async Task CancellationClosesProxyDuringEachHandshakeStage(bool udp, int stage)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var stalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            await stream.ReadExactlyAsync(new byte[3], timeout.Token);
            if (stage >= 1)
            {
                await stream.WriteAsync(new byte[] { 5, 2 }, timeout.Token);
                await stream.ReadExactlyAsync(new byte[5], timeout.Token); // u / p authentication
            }
            if (stage >= 2)
            {
                await stream.WriteAsync(new byte[] { 1, 0 }, timeout.Token);
                await stream.ReadExactlyAsync(new byte[10], timeout.Token); // IPv4 CONNECT / UDP ASSOCIATE
            }
            await stream.WriteAsync(new byte[] { stage == 1 ? (byte)1 : (byte)5 }, timeout.Token);
            stalled.SetResult();
            await (await stream.ReadAsync(new byte[1], timeout.Token)).Should().BeEqualTo(0);
        });
        var rule = new AppRouteRule
        {
            Kind = AppRouteKind.Socks5,
            SocksPort = ((IPEndPoint)listener.LocalEndpoint).Port,
            SocksUsername = "u",
            SocksPassword = "p"
        };
        var errors = new ConcurrentQueue<Exception>();
        using var session = udp ? new RouteUdpSession(rule, new(IPAddress.Loopback, 443),
            (_, _) => { }, stop.Token, errors.Enqueue, () => true) : null;
        async Task Connect()
        {
            using var socket = await RouteConnector.ConnectTcp(rule, new(IPAddress.Loopback, 443), stop.Token);
        }
        var operation = session?.Completion ?? Connect();
        await stalled.Task.WaitAsync(timeout.Token);
        if (session != null)
        {
            session.Dispose();
        }
        else
        {
            stop.Cancel();
        }
        var cancelled = false;
        try { await operation.WaitAsync(timeout.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { cancelled = true; }
        await cancelled.Should().BeEqualTo(!udp);
        await errors.IsEmpty.Should().BeTrue();
        await server.WaitAsync(timeout.Token);
    }

    [Test]
    [Arguments("_connections")]
    [Arguments("_sessions")]
    public async Task EngineShutdownWaitsForRelaysRegisteredByItsLastWorker(string tasksField)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var engine = new AppRouteEngine([], [], _ => { });
        // Stage the final accept/capture iteration without opening WinDivert.
        var workers = Workers(engine);
        var connections = (ConcurrentDictionary<long, Task>)typeof(AppRouteEngine).GetField(tasksField, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        var worker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        workers.Add(worker.Task);
        var disposal = engine.DisposeAsync().AsTask();
        try
        {
            connections[1] = connection.Task;
            worker.SetResult();
            var first = await Task.WhenAny(disposal, Task.Delay(200, timeout.Token));
            await (first == disposal).Should().BeFalse();
        }
        finally
        {
            connection.TrySetResult();
            worker.TrySetResult();
            await disposal.WaitAsync(timeout.Token);
        }
    }
}
