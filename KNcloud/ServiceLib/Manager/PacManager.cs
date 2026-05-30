namespace ServiceLib.Manager;

public class PacManager
{
    private static readonly Lazy<PacManager> _instance = new(() => new PacManager());
    public static PacManager Instance => _instance.Value;

    private int _httpPort;
    private int _pacPort;
    private TcpListener? _tcpListener;
    private byte[] _writeContent;
    private bool _isRunning;
    private bool _needRestart = true;

    public async Task StartAsync(int httpPort, int pacPort)
    {
        _needRestart = httpPort != _httpPort || pacPort != _pacPort || !_isRunning;

        _httpPort = httpPort;
        _pacPort = pacPort;

        await InitText();

        if (_needRestart)
        {
            Stop();
            RunListener();
        }
    }

    private async Task InitText()
    {
        var customSystemProxyPacPath = AppManager.Instance.Config.SystemProxyItem?.CustomSystemProxyPacPath;
        var fileName = (customSystemProxyPacPath.IsNotEmpty() && File.Exists(customSystemProxyPacPath))
            ? customSystemProxyPacPath
            : Path.Combine(Utils.GetConfigPath(), "pac.txt");

        // TODO: temporarily notify which script is being used
        NoticeManager.Instance.SendMessage(fileName);

        if (!File.Exists(fileName))
        {
            var pac = EmbedUtils.GetEmbedText(Global.PacFileName);
            await File.AppendAllTextAsync(fileName, pac);
        }

        var pacText = await File.ReadAllTextAsync(fileName);
        pacText = pacText.Replace("__PROXY__", $"PROXY 127.0.0.1:{_httpPort};DIRECT;");

        var sb = new StringBuilder();
        sb.AppendLine("HTTP/1.0 200 OK");
        sb.AppendLine("Content-type:application/x-ns-proxy-autoconfig");
        sb.AppendLine("Connection:close");
        sb.AppendLine("Content-Length:" + Encoding.UTF8.GetByteCount(pacText));
        sb.AppendLine();
        sb.Append(pacText);
        _writeContent = Encoding.UTF8.GetBytes(sb.ToString());
    }

    private void RunListener()
    {
        _tcpListener = TcpListener.Create(_pacPort);
        _isRunning = true;
        _tcpListener.Start();
        Task.Factory.StartNew(async () =>
        {
            while (_isRunning)
            {
                try
                {
                    if (!_tcpListener.Pending())
                    {
                        await Task.Delay(10);
                        continue;
                    }

                    var client = await _tcpListener.AcceptTcpClientAsync();
                    await Task.Run(() => WriteContent(client));
                }
                catch
                {
                    // ignored
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    private void WriteContent(TcpClient client)
    {
        var stream = client.GetStream();
        stream.Write(_writeContent, 0, _writeContent.Length);
        stream.Flush();
    }

    public void Stop()
    {
        if (_tcpListener == null)
        {
            return;
        }
        try
        {
            _isRunning = false;
            _tcpListener.Stop();
            _tcpListener = null;
        }
        catch
        {
            // ignored
        }
    }
}
