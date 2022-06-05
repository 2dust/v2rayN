using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                this.Success = success;
                this.Msg = msg;
            }
        }

        public async Task<int> DownloadDataAsync(string url, WebProxy webProxy, int downloadTimeout)
        {
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(false, ResUI.Speedtesting));

                var client = new HttpClient(new WebRequestHandler()
                {
                    Proxy = webProxy
                });

                var progress = new Progress<string>();
                progress.ProgressChanged += (sender, value) =>
                {
                    if (UpdateCompleted != null)
                    {
                        string msg = string.Format("{0} M/s", value).PadLeft(9, ' ');
                        UpdateCompleted(this, new ResultEventArgs(false, msg));
                    }
                };

                var cancellationToken = new CancellationTokenSource();
                cancellationToken.CancelAfter(downloadTimeout * 1000);
                await HttpClientHelper.GetInstance().DownloadDataAsync4Speed(client,
                      url,
                      progress,
                      cancellationToken.Token);
            }
            catch (Exception ex)
            {
                //Utils.SaveLog(ex.Message, ex);
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
            return 0;
        }

        public void DownloadFileAsync(string url, bool blProxy, int downloadTimeout)
        {
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(false, ResUI.Downloading));

                var client = new HttpClient(new WebRequestHandler()
                {
                    Proxy = GetWebProxy(blProxy)
                });

                var progress = new Progress<double>();
                progress.ProgressChanged += (sender, value) =>
                {
                    if (UpdateCompleted != null)
                    {
                        string msg = string.Format("...{0}%", value);
                        UpdateCompleted(this, new ResultEventArgs(value > 100 ? true : false, msg));
                    }
                };

                var cancellationToken = new CancellationTokenSource();
                _ = HttpClientHelper.GetInstance().DownloadFileAsync(client,
                       url,
                       Utils.GetPath(Utils.GetDownloadFileName(url)),
                       progress,
                       cancellationToken.Token);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public async Task<string> UrlRedirectAsync(string url, bool blProxy)
        {
            Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);
            WebRequestHandler webRequestHandler = new WebRequestHandler
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

        /// <summary>
        /// DownloadString
        /// </summary> 
        /// <param name="url"></param>
        public async Task<string> DownloadStringAsync(string url, bool blProxy, string userAgent)
        {
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);
                var client = new HttpClient(new WebRequestHandler()
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
            }
            return null;
        }

        public int RunAvailabilityCheck(WebProxy webProxy)
        {
            try
            {
                if (webProxy == null)
                {
                    var httpPort = LazyConfig.Instance.GetConfig().GetLocalPort(Global.InboundHttp);
                    webProxy = new WebProxy(Global.Loopback, httpPort);
                }

                try
                {
                    string status = GetRealPingTime(Global.SpeedPingTestUrl, webProxy, out int responseTime);
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

        public string GetRealPingTime(string url, WebProxy webProxy, out int responseTime)
        {
            string msg = string.Empty;
            responseTime = -1;
            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                myHttpWebRequest.Timeout = 30 * 1000;
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
            var httpPort = LazyConfig.Instance.GetConfig().GetLocalPort(Global.InboundHttp);
            var webProxy = new WebProxy(Global.Loopback, httpPort);
            if (RunAvailabilityCheck(webProxy) > 0)
            {
                return webProxy;
            }

            return null;
        }
    }
}
