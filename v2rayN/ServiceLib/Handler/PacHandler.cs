using System.Net.Sockets;
using System.Text;

namespace ServiceLib.Handler
{
    public class PacHandler
    {
        private static string _configPath;
        private static int _httpPort;
        private static int _pacPort;
        private static TcpListener? _tcpListener;
        private static byte[] _writeContent;
        private static bool _isRunning;
        private static bool _needRestart = true;

        public static async Task Start(string configPath, int httpPort, int pacPort)
        {
            _needRestart = (configPath != _configPath || httpPort != _httpPort || pacPort != _pacPort || !_isRunning);

            _configPath = configPath;
            _httpPort = httpPort;
            _pacPort = pacPort;

            await InitText();

            if (_needRestart)
            {
                Stop();
                RunListener();
            }
        }

        private static async Task InitText()
        {
            var path = Path.Combine(_configPath, "pac.txt");
            if (!File.Exists(path))
            {
                var pac = EmbedUtils.GetEmbedText(Global.PacFileName);
                await File.AppendAllTextAsync(path, pac);
            }

            var pacText =
                (await File.ReadAllTextAsync(path)).Replace("__PROXY__", $"PROXY 127.0.0.1:{_httpPort};DIRECT;");

            var sb = new StringBuilder();
            sb.AppendLine("HTTP/1.0 200 OK");
            sb.AppendLine("Content-type:application/x-ns-proxy-autoconfig");
            sb.AppendLine("Connection:close");
            sb.AppendLine("Content-Length:" + Encoding.UTF8.GetByteCount(pacText));
            sb.AppendLine();
            sb.Append(pacText);
            _writeContent = Encoding.UTF8.GetBytes(sb.ToString());
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

                        var client = await _tcpListener.AcceptTcpClientAsync();
                        await Task.Run(() => { WriteContent(client); });
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static void WriteContent(TcpClient client)
        {
            var stream = client.GetStream();
            stream.Write(_writeContent, 0, _writeContent.Length);
            stream.Flush();
        }

        public static void Stop()
        {
            if (_tcpListener == null)
                return;
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
}
