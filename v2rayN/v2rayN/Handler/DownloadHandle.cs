using System;
using System.IO;
using System.Net;
using v2rayN.Base;

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

        private int progressPercentage = -1;
        private long totalBytesToReceive = 0;
        private DateTime totalDatetime = new DateTime();
        private int DownloadTimeout = -1;

        public WebClientEx DownloadFileAsync(string url, WebProxy webProxy, int downloadTimeout)
        {
            WebClientEx ws = new WebClientEx();
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(false, UIRes.I18N("Downloading")));

                progressPercentage = -1;
                totalBytesToReceive = 0;

                //WebClientEx ws = new WebClientEx();
                DownloadTimeout = downloadTimeout;
                if (webProxy != null)
                {
                    ws.Proxy = webProxy;// new WebProxy(Global.Loopback, Global.httpPort);
                }

                ws.DownloadFileCompleted += ws_DownloadFileCompleted;
                ws.DownloadProgressChanged += ws_DownloadProgressChanged;
                ws.DownloadFileAsync(new Uri(url), Utils.GetPath(Utils.GetDownloadFileName(url)));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
            return ws;
        }

        void ws_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (UpdateCompleted != null)
            {
                if (totalBytesToReceive == 0)
                {
                    totalDatetime = DateTime.Now;
                    totalBytesToReceive = e.BytesReceived;
                    return;
                }
                totalBytesToReceive = e.BytesReceived;

                if (DownloadTimeout != -1)
                {
                    if ((DateTime.Now - totalDatetime).TotalSeconds > DownloadTimeout)
                    {
                        ((WebClientEx)sender).CancelAsync();
                    }
                }
                if (progressPercentage != e.ProgressPercentage && e.ProgressPercentage % 10 == 0)
                {
                    progressPercentage = e.ProgressPercentage;
                    string msg = string.Format("...{0}%", e.ProgressPercentage);
                    UpdateCompleted(this, new ResultEventArgs(false, msg));
                }
            }
        }
        void ws_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                if (UpdateCompleted != null)
                {
                    if (e.Cancelled)
                    {
                        ((WebClientEx)sender).Dispose();
                        TimeSpan ts = (DateTime.Now - totalDatetime);
                        string speed = string.Format("{0} M/s", (totalBytesToReceive / ts.TotalMilliseconds / 1000).ToString("#0.0"));
                        UpdateCompleted(this, new ResultEventArgs(true, speed.PadLeft(8, ' ')));
                        return;
                    }

                    if (e.Error == null
                        || Utils.IsNullOrEmpty(e.Error.ToString()))
                    {
                        ((WebClientEx)sender).Dispose();
                        TimeSpan ts = (DateTime.Now - totalDatetime);
                        string speed = string.Format("{0} M/s", (totalBytesToReceive / ts.TotalMilliseconds / 1000).ToString("#0.0"));
                        UpdateCompleted(this, new ResultEventArgs(true, speed.PadLeft(8, ' ')));
                    }
                    else
                    {
                        throw e.Error;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// DownloadString
        /// </summary> 
        /// <param name="url"></param>
        public void WebDownloadString(string url, WebProxy webProxy, string userAgent)
        {
            string source = string.Empty;
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);

                WebClientEx ws = new WebClientEx();
                if (webProxy != null)
                {
                    ws.Proxy = webProxy;
                }

                if (Utils.IsNullOrEmpty(userAgent))
                {
                    userAgent = $"{Utils.GetVersion(false)}";
                }
                ws.Headers.Add("user-agent", userAgent);

                Uri uri = new Uri(url);
                //Authorization Header
                if (!Utils.IsNullOrEmpty(uri.UserInfo))
                {
                    ws.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Utils.Base64Encode(uri.UserInfo));
                }

                ws.DownloadStringCompleted += Ws_DownloadStringCompleted;
                ws.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private void Ws_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (e.Error == null
                    || Utils.IsNullOrEmpty(e.Error.ToString()))
                {
                    string source = e.Result;
                    UpdateCompleted?.Invoke(this, new ResultEventArgs(true, source));
                }
                else
                {
                    throw e.Error;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public string WebDownloadStringSync(string url)
        {
            string source = string.Empty;
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);

                WebClientEx ws = new WebClientEx();

                return ws.DownloadString(new Uri(url));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return string.Empty;
            }
        }

        public WebClientEx DownloadDataAsync(string url, WebProxy webProxy, int downloadTimeout)
        {
            WebClientEx ws = new WebClientEx();
            try
            {
                Utils.SetSecurityProtocol(LazyConfig.Instance.GetConfig().enableSecurityProtocolTls13);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(false, UIRes.I18N("Downloading")));

                progressPercentage = -1;
                totalBytesToReceive = 0;

                DownloadTimeout = downloadTimeout;
                if (webProxy != null)
                {
                    ws.Proxy = webProxy;
                }

                ws.DownloadProgressChanged += ws_DownloadProgressChanged;
                ws.DownloadDataCompleted += ws_DownloadFileCompleted;
                ws.DownloadDataAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
            return ws;
        }
    }
}
