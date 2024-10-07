using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace ServiceLib.Handler
{
    /// <summary>
    ///Download
    /// </summary>
    public class DownloadHandler
    {
        public event EventHandler<ResultEventArgs>? UpdateCompleted;

        public event ErrorEventHandler? Error;

        public class ResultEventArgs : EventArgs
        {
            public bool Success;
            public string Msg;

            public ResultEventArgs(bool success, string msg)
            {
                Success = success;
                Msg = msg;
            }
        }

        public async Task<int> DownloadDataAsync(string url, WebProxy webProxy, int downloadTimeout, Action<bool, string> update)
        {
            try
            {
                Utils.SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);

                var progress = new Progress<string>();
                progress.ProgressChanged += (sender, value) =>
                {
                    if (update != null)
                    {
                        string msg = $"{value}";
                        update(false, msg);
                    }
                };

                await DownloaderHelper.Instance.DownloadDataAsync4Speed(webProxy,
                      url,
                      progress,
                      downloadTimeout);
            }
            catch (Exception ex)
            {
                update(false, ex.Message);
                if (ex.InnerException != null)
                {
                    update(false, ex.InnerException.Message);
                }
            }
            return 0;
        }

        public async Task DownloadFileAsync(string url, string fileName, bool blProxy, int downloadTimeout)
        {
            try
            {
                Utils.SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(false, $"{ResUI.Downloading}   {url}"));

                var progress = new Progress<double>();
                progress.ProgressChanged += (sender, value) =>
                {
                    UpdateCompleted?.Invoke(this, new ResultEventArgs(value > 100, $"...{value}%"));
                };

                var webProxy = GetWebProxy(blProxy);
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
            Utils.SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);
            var webRequestHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                Proxy = GetWebProxy(blProxy)
            };
            HttpClient client = new(webRequestHandler);

            HttpResponseMessage response = await client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is not null)
            {
                return response.Headers.Location.ToString();
            }
            else
            {
                Logging.SaveLog("StatusCode error: " + url);
                return null;
            }
        }

        public async Task<string?> TryDownloadString(string url, bool blProxy, string userAgent)
        {
            try
            {
                var result1 = await DownloadStringAsync(url, blProxy, userAgent);
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
                var result2 = await DownloadStringViaDownloader(url, blProxy, userAgent);
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

            try
            {
                using var wc = new WebClient();
                wc.Proxy = GetWebProxy(blProxy);
                var result3 = await wc.DownloadStringTaskAsync(url);
                if (Utils.IsNotEmpty(result3))
                {
                    return result3;
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
        public async Task<string?> DownloadStringAsync(string url, bool blProxy, string userAgent)
        {
            try
            {
                Utils.SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);
                var webProxy = GetWebProxy(blProxy);
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
                var result = await HttpClientHelper.Instance.GetAsync(client, url, cts.Token).WaitAsync(TimeSpan.FromSeconds(30), cts.Token);
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
        public async Task<string?> DownloadStringViaDownloader(string url, bool blProxy, string userAgent)
        {
            try
            {
                Utils.SetSecurityProtocol(AppHandler.Instance.Config.guiItem.enableSecurityProtocolTls13);

                var webProxy = GetWebProxy(blProxy);

                if (Utils.IsNullOrEmpty(userAgent))
                {
                    userAgent = Utils.GetVersion(false);
                }
                var result = await DownloaderHelper.Instance.DownloadStringAsync(webProxy, url, userAgent, 30);
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
                if (webProxy == null)
                {
                    webProxy = GetWebProxy(true);
                }

                try
                {
                    var config = AppHandler.Instance.Config;
                    int responseTime = await GetRealPingTime(config.speedTestItem.speedPingTestUrl, webProxy, 10);
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
            int responseTime = -1;
            try
            {
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(downloadTimeout));
                using var client = new HttpClient(new SocketsHttpHandler()
                {
                    Proxy = webProxy,
                    UseProxy = webProxy != null
                });

                List<int> oneTime = [];
                for (int i = 0; i < 2; i++)
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

        private WebProxy? GetWebProxy(bool blProxy)
        {
            if (!blProxy)
            {
                return null;
            }
            var httpPort = AppHandler.Instance.GetLocalPort(EInboundProtocol.http);
            if (!SocketCheck(Global.Loopback, httpPort))
            {
                return null;
            }

            return new WebProxy(Global.Loopback, httpPort);
        }

        private bool SocketCheck(string ip, int port)
        {
            try
            {
                IPEndPoint point = new(IPAddress.Parse(ip), port);
                using Socket? sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(point);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}