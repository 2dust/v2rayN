namespace ServiceLib.Tests.Services;

public class CustomCoreIntegrationTests
{
    [Test]
    [Arguments(ECoreType.Xray, "xray", "xray.exe")]
    [Arguments(ECoreType.sing_box, "sing_box", "sing-box.exe")]
    public async Task TemporaryCustomConfigServesSocksRequest(ECoreType coreType, string coreDirectory, string executable)
    {
        // The release core bundle is optional in CI. Set this to its bin directory to run
        // a real, local-only integration check without depending on an external server.
        var binDirectory = Environment.GetEnvironmentVariable("V2RAYN_TEST_CORE_BIN");
        if (string.IsNullOrEmpty(binDirectory))
        {
            return;
        }

        var executablePath = Path.Combine(binDirectory, coreDirectory, executable);
        await File.Exists(executablePath).Should().BeTrue();
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var targetPort = ((IPEndPoint)server.LocalEndpoint).Port;
        var testPorts = GetUnusedPorts(coreType == ECoreType.Xray ? 2 : 1);
        var proxyPort = testPorts[0];
        var configPath = Path.Combine(Path.GetTempPath(), $"v2rayN-custom-test-{Guid.NewGuid():N}.json");
        var source = coreType == ECoreType.Xray
            ? """
              {"inbounds":[{"protocol":"socks","listen":"127.0.0.1","port":10808,"settings":{"auth":"noauth"}},{"protocol":"http","listen":"127.0.0.1","port":10809}],"outbounds":[{"protocol":"freedom"}]}
              """
            : """
              {"inbounds":[{"type":"socks","listen":"127.0.0.1","listen_port":10808}],"outbounds":[{"type":"direct","tag":"direct"}]}
              """;
        var prepared = CustomSpeedtestConfig.TryChangePorts(source, coreType, testPorts, false,
            out var testConfig, out var selectedPort, out _);
        await prepared.Should().BeTrue();
        await selectedPort.Should().BeEqualTo(proxyPort);
        await File.WriteAllTextAsync(configPath, testConfig);

        using var process = new ProcessService(
            executablePath,
            coreType == ECoreType.Xray ? $"run -c \"{configPath}\"" : $"run -c \"{configPath}\" --disable-color",
            Path.GetDirectoryName(executablePath)!,
            displayLog: false,
            redirectInput: false,
            environmentVars: null,
            updateFunc: null);
        try
        {
            await process.StartAsync();
            process.OwnTemporaryResources(configPath);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var serverTask = ReplyToHttpRequest(server, timeout.Token);

            using var proxy = await ConnectToProxy(proxyPort, process, timeout.Token);
            var stream = proxy.GetStream();
            await stream.WriteAsync(new byte[] { 5, 1, 0 }, timeout.Token);
            var greeting = new byte[2];
            await stream.ReadExactlyAsync(greeting, timeout.Token);
            await greeting.SequenceEqual(new byte[] { 5, 0 }).Should().BeTrue();

            var connect = new byte[] { 5, 1, 0, 1, 127, 0, 0, 1,
                unchecked((byte)(targetPort >> 8)), unchecked((byte)targetPort) };
            await stream.WriteAsync(connect, timeout.Token);
            var response = new byte[10];
            await stream.ReadExactlyAsync(response, timeout.Token);
            await response[1].Should().BeEqualTo((byte)0);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"), timeout.Token);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            var responseText = await reader.ReadToEndAsync(timeout.Token);
            await responseText.Should().Contain("200 OK");
            await responseText.Should().Contain("custom-ok");
            await serverTask;
        }
        finally
        {
            try
            {
                await process.StopAsync();
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }
        await File.Exists(configPath).Should().BeFalse();
    }

    private static List<int> GetUnusedPorts(int count)
    {
        var ports = new HashSet<int>();
        while (ports.Count < count)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            ports.Add(((IPEndPoint)listener.LocalEndpoint).Port);
            listener.Stop();
        }
        return [.. ports];
    }

    private static async Task<TcpClient> ConnectToProxy(int port, ProcessService process, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException("The test core exited before its SOCKS inbound became ready.");
            }
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, ct);
                return client;
            }
            catch (SocketException)
            {
                client.Dispose();
                await Task.Delay(100, ct);
            }
        }
    }

    private static async Task ReplyToHttpRequest(TcpListener server, CancellationToken ct)
    {
        using var client = await server.AcceptTcpClientAsync(ct);
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        while (await reader.ReadLineAsync(ct) is { Length: > 0 })
        {
        }
        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 9\r\nConnection: close\r\n\r\ncustom-ok"), ct);
    }
}
