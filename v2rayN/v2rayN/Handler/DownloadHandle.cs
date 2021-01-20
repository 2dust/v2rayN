using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        #region Check for updates

        private readonly string nLatestUrl = Global.NUrl + "/latest";
        private const string nUrl = Global.NUrl + "/download/{0}/v2rayN.zip";
        private readonly string v2flyCoreLatestUrl = Global.v2flyCoreUrl + "/latest";
        private const string v2flyCoreUrl = Global.v2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip";
        private readonly string xrayCoreLatestUrl = Global.xrayCoreUrl + "/latest";
        private const string xrayCoreUrl = Global.xrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip";

        public async void CheckUpdateAsync(string type)
        {
            Utils.SetSecurityProtocol();
            WebRequestHandler webRequestHandler = new WebRequestHandler
            {
                AllowAutoRedirect = false
            };
            HttpClient httpClient = new HttpClient(webRequestHandler);

            string url;
            if (type == "v2fly")
            {
                url = v2flyCoreLatestUrl;
            }
            else if (type == "xray")
            {
                url = xrayCoreLatestUrl;
            }
            else if (type == "v2rayN")
            {
                url = nLatestUrl;
            }
            else
            {
                throw new ArgumentException("Type");
            }
            HttpResponseMessage response = await httpClient.GetAsync(url);
            if (response.StatusCode.ToString() == "Redirect")
            {
                responseHandler(type, response.Headers.Location.ToString());
            }
            else
            {
                Utils.SaveLog("StatusCode error: " + url);
                return;
            }
        }

        /// <summary>
        /// 获取V2RayCore版本
        /// </summary>
        public string getCoreVersion(string type)
        {
            try
            {
                var core = string.Empty;
                var match = string.Empty;
                if (type == "v2fly")
                {
                    core = "v2ray.exe";
                    match = "V2Ray";
                }
                else if (type == "xray")
                {
                    core = "xray.exe";
                    match = "Xray";
                }
                string filePath = Utils.GetPath(core);
                if (!File.Exists(filePath))
                {
                    string msg = string.Format(UIRes.I18N("NotFoundCore"), @"");
                    //ShowMsg(true, msg);
                    return "";
                }

                Process p = new Process();
                p.StartInfo.FileName = filePath;
                p.StartInfo.Arguments = "-version";
                p.StartInfo.WorkingDirectory = Utils.StartupPath();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.Start();
                p.WaitForExit(5000);
                string echo = p.StandardOutput.ReadToEnd();
                string version = Regex.Match(echo, $"{match} ([0-9.]+) \\(").Groups[1].Value;
                return version;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return "";
            }
        }
        private void responseHandler(string type, string redirectUrl)
        {
            try
            {
                string version = redirectUrl.Substring(redirectUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);

                string curVersion;
                string message;
                string url;
                if (type == "v2fly")
                {
                    curVersion = "v" + getCoreVersion(type);
                    message = string.Format(UIRes.I18N("IsLatestCore"), curVersion);
                    string osBit = Environment.Is64BitProcess ? "64" : "32";
                    url = string.Format(v2flyCoreUrl, version, osBit);
                }
                else if (type == "xray")
                {
                    curVersion = "v" + getCoreVersion(type);
                    message = string.Format(UIRes.I18N("IsLatestCore"), curVersion);
                    string osBit = Environment.Is64BitProcess ? "64" : "32";
                    url = string.Format(xrayCoreUrl, version, osBit);
                }
                else if (type == "v2rayN")
                {
                    curVersion = FileVersionInfo.GetVersionInfo(Utils.GetExePath()).FileVersion.ToString();
                    message = string.Format(UIRes.I18N("IsLatestN"), curVersion);
                    url = string.Format(nUrl, version);
                }
                else
                {
                    throw new ArgumentException("Type");
                }

                if (curVersion == version)
                {
                    AbsoluteCompleted?.Invoke(this, new ResultEventArgs(false, message));
                    return;
                }

                AbsoluteCompleted?.Invoke(this, new ResultEventArgs(true, url));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        #endregion

        #region Download 

        public WebClientEx DownloadFileAsync(string url, WebProxy webProxy, int downloadTimeout)
        {
            WebClientEx ws = new WebClientEx();
            try
            {
                Utils.SetSecurityProtocol();
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
                ws.DownloadFileAsync(new Uri(url), Utils.GetPath(DownloadFileName));
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
                Utils.SetSecurityProtocol();

                WebClientEx ws = new WebClientEx();

                return ws.DownloadString(new Uri(url));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return string.Empty;
            }
        }
        #endregion

    }
}
