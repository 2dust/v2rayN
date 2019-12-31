using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Properties;

namespace v2rayN.Handler
{
    /// <summary>
    ///Download
    /// </summary>
    class DownloadHandle
    {
        public event EventHandler<ResultEventArgs> AbsoluteCompleted;

        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public string DownloadFileName
        {
            get
            {
                return "v2ray-windows.zip";
            }
        }

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



        #region v2rayN

        private string nLatestUrl = "https://github.com/2dust/v2rayN/releases/latest";
        private const string nUrl = "https://github.com/2dust/v2rayN/releases/download/{0}/v2rayN.zip";

        public void AbsoluteV2rayN(Config config)
        {
            Utils.SetSecurityProtocol();
            WebRequest request = WebRequest.Create(nLatestUrl);
            request.BeginGetResponse(new AsyncCallback(OnResponseV2rayN), request);
        }

        private void OnResponseV2rayN(IAsyncResult ar)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)ar.AsyncState;
                HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(ar);
                string redirectUrl = response.ResponseUri.AbsoluteUri;
                string version = redirectUrl.Substring(redirectUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);

                var curVersion = FileVersionInfo.GetVersionInfo(Utils.GetExePath()).FileVersion.ToString();
                if (curVersion == version)
                {
                    if (AbsoluteCompleted != null)
                    {
                        AbsoluteCompleted(this, new ResultEventArgs(false, "Already the latest version"));
                    }
                    return;
                }

                string url = string.Format(nUrl, version);
                if (AbsoluteCompleted != null)
                {
                    AbsoluteCompleted(this, new ResultEventArgs(true, url));
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
        }

        #endregion

        #region Core

        private string coreLatestUrl = "https://github.com/v2ray/v2ray-core/releases/latest";
        private const string coreUrl = "https://github.com/v2ray/v2ray-core/releases/download/{0}/v2ray-windows-{1}.zip";

        public void AbsoluteV2rayCore(Config config)
        {
            Utils.SetSecurityProtocol();
            WebRequest request = WebRequest.Create(coreLatestUrl);
            request.BeginGetResponse(new AsyncCallback(OnResponseV2rayCore), request);
        }

        private void OnResponseV2rayCore(IAsyncResult ar)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)ar.AsyncState;
                HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(ar);
                string redirectUrl = response.ResponseUri.AbsoluteUri;
                string version = redirectUrl.Substring(redirectUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);

                string osBit = string.Empty;
                if (Environment.Is64BitProcess)
                {
                    osBit = "64";
                }
                else
                {
                    osBit = "32";
                }
                string url = string.Format(coreUrl, version, osBit);
                if (AbsoluteCompleted != null)
                {
                    AbsoluteCompleted(this, new ResultEventArgs(true, url));
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
        }

        #endregion

        #region Download 

        public void DownloadFileAsync(Config config, string url, WebProxy webProxy, int downloadTimeout)
        {
            try
            {
                Utils.SetSecurityProtocol();
                if (UpdateCompleted != null)
                {
                    UpdateCompleted(this, new ResultEventArgs(false, "Downloading..."));
                }

                progressPercentage = -1;
                totalBytesToReceive = 0;

                WebClientEx ws = new WebClientEx();
                DownloadTimeout = downloadTimeout;
                if (webProxy != null)
                {
                    ws.Proxy = webProxy;// new WebProxy(Global.Loopback, Global.httpPort);
                }

                ws.DownloadFileCompleted += ws_DownloadFileCompleted;
                ws.DownloadProgressChanged += ws_DownloadProgressChanged;
                ws.DownloadFileAsync(new Uri(url), Utils.GetPath(DownloadFileName));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
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
                        string speed = string.Format("<{0} M/s", (totalBytesToReceive / ts.TotalMilliseconds / 1000).ToString("#0.##"));
                        UpdateCompleted(this, new ResultEventArgs(true, speed));
                        return;
                    }

                    if (e.Error == null
                        || Utils.IsNullOrEmpty(e.Error.ToString()))
                    {

                        TimeSpan ts = (DateTime.Now - totalDatetime);
                        string speed = string.Format("{0} M/s", (totalBytesToReceive / ts.TotalMilliseconds / 1000).ToString("#0.##"));
                        UpdateCompleted(this, new ResultEventArgs(true, speed));
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

                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// DownloadString
        /// </summary> 
        /// <param name="url"></param>
        public void WebDownloadString(string url)
        {
            string source = string.Empty;
            try
            {
                Utils.SetSecurityProtocol();

                WebClientEx ws = new WebClientEx();
                ws.DownloadStringCompleted += Ws_DownloadStringCompleted;
                ws.DownloadStringAsync(new Uri(url));
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
                    if (UpdateCompleted != null)
                    {
                        UpdateCompleted(this, new ResultEventArgs(true, source));
                    }
                }
                else
                {
                    throw e.Error;
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
        }

        #endregion

        #region PAC

        public string GenPacFile(string result)
        {
            try
            {
                File.WriteAllText(Utils.GetTempPath("gfwlist.txt"), result, Encoding.UTF8);
                List<string> lines = ParsePacResult(result);
                string abpContent = Utils.UnGzip(Resources.abp_js);
                abpContent = abpContent.Replace("__RULES__", JsonConvert.SerializeObject(lines, Formatting.Indented));
                File.WriteAllText(Utils.GetPath(Global.pacFILE), abpContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return ex.Message;
            }
            return string.Empty;
        }

        private List<string> ParsePacResult(string response)
        {
            IEnumerable<char> IgnoredLineBegins = new[] { '!', '[' };

            byte[] bytes = Convert.FromBase64String(response);
            string content = Encoding.UTF8.GetString(bytes);
            List<string> valid_lines = new List<string>();
            using (var sr = new StringReader(content))
            {
                foreach (var line in sr.NonWhiteSpaceLines())
                {
                    if (line.BeginWithAny(IgnoredLineBegins))
                        continue;
                    valid_lines.Add(line);
                }
            }
            return valid_lines;
        }

        #endregion
    }
}
