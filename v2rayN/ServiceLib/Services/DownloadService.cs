using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace ServiceLib.Services
{
    /// <summary>
    ///Download
    /// </summary>
    public class DownloadService
    {
        public event EventHandler<RetResult>? UpdateCompleted;

        public event ErrorEventHandler? Error;

        public async Task<int> DownloadDataAsync(string url, WebProxy webProxy, int downloadTimeout, Action<bool, string> updateFunc)
        {
            try
            {
                SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);

                var progress = new Progress<string>();
                progress.ProgressChanged += (sender, value) =>
                {
                    if (updateFunc != null)
                    {
                        string msg = $"{value}";
                        updateFunc?.Invoke(false, msg);
                    }
                };

                await DownloaderHelper.Instance.DownloadDataAsync4Speed(webProxy,
                      url,
                      progress,
                      downloadTimeout);
            }
            catch (Exception ex)
            {
                updateFunc?.Invoke(false, ex.Message);
                if (ex.InnerException != null)
                {
                    updateFunc?.Invoke(false, ex.InnerException.Message);
                }
            }
            return 0;
        }

        public async Task DownloadFileAsync(string url, string fileName, bool blProxy, int downloadTimeout)
        {
            try
            {
                SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new RetResult(false, $"{ResUI.Downloading}   {url}"));

                var progress = new Progress<double>();
                progress.ProgressChanged += (sender, value) =>
                {
                    UpdateCompleted?.Invoke(this, new RetResult(value > 100, $"...{value}%"));
                };

                var webProxy = await GetWebProxy(blProxy);
                await DownloaderHelper.Instance.DownloadFileAsync(webProxy,
                    url,
                    fileName,
                    progress,
                    downloadTimeout);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }
        }

        public async Task<string?> UrlRedirectAsync(string url, bool blProxy)
        {
            SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);
            var webRequestHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                Proxy = await GetWebProxy(blProxy)
            };
            HttpClient client = new(webRequestHandler);

            var response = await client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is not null)
            {
                return response.Headers.Location.ToString();
            }
            else
            {
                Error?.Invoke(this, new ErrorEventArgs(new Exception("StatusCode error: " + response.StatusCode)));
                Logging.SaveLog("StatusCode error: " + url);
                return null;
            }
        }

        public async Task<string?> TryDownloadString(string url, bool blProxy, string userAgent)
        {
            try
            {
                var result1 = await DownloadStringAsync(url, blProxy, userAgent, 15);
                if (Utils.IsNotEmpty(result1))
                {
                    return result1;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }

            try
            {
                var result2 = await DownloadStringViaDownloader(url, blProxy, userAgent, 15);
                if (Utils.IsNotEmpty(result2))
                {
                    return result2;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }

            return null;
        }

        /// <summary>
        /// DownloadString
        /// </summary>
        /// <param name="url"></param>
        private async Task<string?> DownloadStringAsync(string url, bool blProxy, string userAgent, int timeout)
        {
            try
            {
                SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);
                var webProxy = await GetWebProxy(blProxy);
                var client = new HttpClient(new SocketsHttpHandler()
                {
                    Proxy = webProxy,
                    UseProxy = webProxy != null
                });

                if (Utils.IsNullOrEmpty(userAgent))
                {
                    userAgent = Utils.GetVersion(false);
                }
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);

                Uri uri = new(url);
                //Authorization Header
                if (Utils.IsNotEmpty(uri.UserInfo))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
                }

                using var cts = new CancellationTokenSource();
                var result = await HttpClientHelper.Instance.GetAsync(client, url, cts.Token).WaitAsync(TimeSpan.FromSeconds(timeout), cts.Token);
                return result;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }
            return null;
        }

        /// <summary>
        /// DownloadString
        /// </summary>
        /// <param name="url"></param>
        private async Task<string?> DownloadStringViaDownloader(string url, bool blProxy, string userAgent, int timeout)
        {
            try
            {
                SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);

                var webProxy = await GetWebProxy(blProxy);

                if (Utils.IsNullOrEmpty(userAgent))
                {
                    userAgent = Utils.GetVersion(false);
                }
                var result = await DownloaderHelper.Instance.DownloadStringAsync(webProxy, url, userAgent, timeout);
                return result;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }
            return null;
        }

        public async Task<int> RunAvailabilityCheck(IWebProxy? webProxy)
        {
            try
            {
                webProxy ??= await GetWebProxy(true);

                try
                {
                    var config = AppHandler.Instance.Config;
                    var responseTime = await GetRealPingTime(config.speedTestItem.speedPingTestUrl, webProxy, 10);
                    return responseTime;
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(ex.Message, ex);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                return -1;
            }
        }

        public async Task<int> GetRealPingTime(string url, IWebProxy? webProxy, int downloadTimeout)
        {
            var responseTime = -1;
            try
            {
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(downloadTimeout));
                using var client = new HttpClient(new SocketsHttpHandler()
                {
                    Proxy = webProxy,
                    UseProxy = webProxy != null
                });

                List<int> oneTime = new();
                for (var i = 0; i < 2; i++)
                {
                    var timer = Stopwatch.StartNew();
                    await client.GetAsync(url, cts.Token);
                    timer.Stop();
                    oneTime.Add((int)timer.Elapsed.TotalMilliseconds);
                    await Task.Delay(100);
                }
                responseTime = oneTime.Where(x => x > 0).OrderBy(x => x).FirstOrDefault();
            }
            catch //(Exception ex)
            {
                //Utile.SaveLog(ex.Message, ex);
            }
            return responseTime;
        }

        private async Task<WebProxy?> GetWebProxy(bool blProxy)
        {
            if (!blProxy)
            {
                return null;
            }
            var httpPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.http);
            if (await SocketCheck(Global.Loopback, httpPort) == false)
            {
                return null;
            }

            return new WebProxy(Global.Loopback, httpPort);
        }

        private async Task<bool> SocketCheck(string ip, int port)
        {
            try
            {
                IPEndPoint point = new(IPAddress.Parse(ip), port);
                using Socket? sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await sock.ConnectAsync(point);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SetSecurityProtocol(bool enableSecurityProtocolTls13)
        {
            if (enableSecurityProtocolTls13)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            }
            else
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            ServicePointManager.DefaultConnectionLimit = 256;
        }
    }
}