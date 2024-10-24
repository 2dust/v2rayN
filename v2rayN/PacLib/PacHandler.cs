using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PacLib;

public class PacHandler
{
    private static string _configPath;
    private static int _httpPort;
    private static int _pacPort;
    private static TcpListener? _tcpListener;
    private static string _pacText;
    private static bool _isRunning;
    private static bool _needRestart = true;

    public static void Start(string configPath, int httpPort, int pacPort)
    {
        _needRestart = (configPath != _configPath || httpPort != _httpPort || pacPort != _pacPort || !_isRunning);

        _configPath = configPath;
        _httpPort = httpPort;
        _pacPort = pacPort;

        InitText();

        if (_needRestart)
        {
            Stop();
            RunListener();
        }
    }

    private static void InitText()
    {
        var path = Path.Combine(_configPath, "pac.txt");
        if (!File.Exists(path))
        {
            File.AppendAllText(path, Resources.ResourceManager.GetString("pac"));
        }

        _pacText = File.ReadAllText(path).Replace("__PROXY__", $"PROXY 127.0.0.1:{_httpPort};DIRECT;");
    }

    private static void RunListener()
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

                    var client = _tcpListener.AcceptTcpClient();
                    await Task.Run(() =>
                      {
                          var stream = client.GetStream();
                          var sb = new StringBuilder();
                          sb.AppendLine("HTTP/1.0 200 OK");
                          sb.AppendLine("Content-type:application/x-ns-proxy-autoconfig");
                          sb.AppendLine("Connection:close");
                          sb.AppendLine("Content-Length:" + Encoding.UTF8.GetByteCount(_pacText));
                          sb.AppendLine();
                          sb.Append(_pacText);
                          var content = Encoding.UTF8.GetBytes(sb.ToString());
                          stream.Write(content, 0, content.Length);
                          stream.Flush();
                      });
                }
                catch
                {
                    // ignored
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    public static void Stop()
    {
        if (_tcpListener == null) return;
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