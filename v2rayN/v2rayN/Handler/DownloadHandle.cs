using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using v2rayN.Base;
using v2rayN.Resx;

namespace v2rayN.Handler
{
    /// <summary>
    ///Download
    /// </summary>
    class DownloadHandle
    {
        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;


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
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().guiItem.enableSecurityProtocolTls13);

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

        public void DownloadFileAsync(string url, bool blProxy, int downloadTimeout)
        {
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().guiItem.enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(false, ResUI.Downloading));

                var progress = new Progress<double>();
                progress.ProgressChanged += (sender, value) =>
                {
                    if (UpdateCompleted != null)
                    {
                        string msg = $"...{value}%";
                        UpdateCompleted(this, new ResultEventArgs(value > 100 ? true : false, msg));
                    }
                };

                var webProxy = GetWebProxy(blProxy);
                _ = DownloaderHelper.Instance.DownloadFileAsync(webProxy,
                    url,
                    Utils.GetTempPath(Utils.GetDownloadFileName(url)),
                    progress,
                    downloadTimeout);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }
        }

        public async Task<string> UrlRedirectAsync(string url, bool blProxy)
        {
            Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().guiItem.enableSecurityProtocolTls13);
            var webRequestHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                Proxy = GetWebProxy(blProxy)
            };
            HttpClient client = new HttpClient(webRequestHandler);

            HttpResponseMessage response = await client.GetAsync(url);
            if (response.StatusCode.ToString() == "Redirect")
            {
                return response.Headers.Location.ToString();
            }
            else
            {
                Utils.SaveLog("StatusCode error: " + url);
                return null;
            }
        }

        public async Task<string> TryDownloadString(string url, bool blProxy, string userAgent)
        {
            try
            {
                var result1 = await DownloadStringAsync(url, blProxy, userAgent);
                if (!Utils.IsNullOrEmpty(result1))
                {
                    return result1;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }

            try
            {
                var result2 = await DownloadStringViaDownloader(url, blProxy, userAgent);
                if (!Utils.IsNullOrEmpty(result2))
                {
                    return result2;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }

            try
            {
                using (var wc = new WebClient())
                {
                    wc.Proxy = GetWebProxy(blProxy);
                    var result3 = await wc.DownloadStringTaskAsync(url);
                    if (!Utils.IsNullOrEmpty(result3))
                    {
                        return result3;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
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
        public async Task<string> DownloadStringAsync(string url, bool blProxy, string userAgent)
        {
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().guiItem.enableSecurityProtocolTls13);
                var client = new HttpClient(new SocketsHttpHandler()
                {
                    Proxy = GetWebProxy(blProxy)
                });

                if (Utils.IsNullOrEmpty(userAgent))
                {
                    userAgent = $"{Utils.GetVersion(false)}";
                }
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);

                Uri uri = new Uri(url);
                //Authorization Header
                if (!Utils.IsNullOrEmpty(uri.UserInfo))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
                }

                var cts = new CancellationTokenSource();
                cts.CancelAfter(1000 * 30);

                var result = await HttpClientHelper.GetInstance().GetAsync(client, url, cts.Token);
                return result;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
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
        public async Task<string> DownloadStringViaDownloader(string url, bool blProxy, string userAgent)
        {
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().guiItem.enableSecurityProtocolTls13);

                var webProxy = GetWebProxy(blProxy);

                if (Utils.IsNullOrEmpty(userAgent))
                {
                    userAgent = $"{Utils.GetVersion(false)}";
                }
                var result = await DownloaderHelper.Instance.DownloadStringAsync(webProxy, url, userAgent, 30);
                return result;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
                if (ex.InnerException != null)
                {
                    Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
                }
            }
            return null;
        }


        public int RunAvailabilityCheck(WebProxy webProxy)
        {
            try
            {
                if (webProxy == null)
                {
                    var httpPort = LazyConfig.Instance.GetLocalPort(Global.InboundHttp);
                    webProxy = new WebProxy(Global.Loopback, httpPort);
                }

                try
                {
                    var config = LazyConfig.Instance.GetConfig();
                    string status = GetRealPingTime(config.speedTestItem.speedPingTestUrl, webProxy, 10, out int responseTime);
                    bool noError = Utils.IsNullOrEmpty(status);
                    return noError ? responseTime : -1;
                }
                catch (Exception ex)
                {
                    Utils.SaveLog(ex.Message, ex);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return -1;
            }
        }

        public string GetRealPingTime(string url, WebProxy webProxy, int downloadTimeout, out int responseTime)
        {
            string msg = string.Empty;
            responseTime = -1;
            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                myHttpWebRequest.Timeout = downloadTimeout * 1000;
                myHttpWebRequest.Proxy = webProxy;

                Stopwatch timer = new Stopwatch();
                timer.Start();

                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                if (myHttpWebResponse.StatusCode != HttpStatusCode.OK
                    && myHttpWebResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    msg = myHttpWebResponse.StatusDescription;
                }
                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;

                myHttpWebResponse.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ex.Message;
            }
            return msg;
        }

        private WebProxy GetWebProxy(bool blProxy)
        {
            if (!blProxy)
            {
                return null;
            }
            var httpPort = LazyConfig.Instance.GetLocalPort(Global.InboundHttp);
            if (!SocketCheck(Global.Loopback, httpPort))
            {
                return null;
            }

            return new WebProxy(Global.Loopback, httpPort);
        }

        private bool SocketCheck(string ip, int port)
        {
            Socket sock = null;
            try
            {
                IPAddress ipa = IPAddress.Parse(ip);
                IPEndPoint point = new IPEndPoint(ipa, port);
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(point);
                return true;
            }
            catch { }
            finally
            {
                if (sock != null)
                {
                    sock.Close();
                    sock.Dispose();
                }
            }
            return false;
        }
    }
}
