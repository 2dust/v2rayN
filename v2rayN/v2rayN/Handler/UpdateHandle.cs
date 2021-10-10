using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class UpdateHandle
    {
        Action<bool, string> _updateFunc;
        private Config _config;

        public event EventHandler<ResultEventArgs> AbsoluteCompleted;

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

        private readonly string nLatestUrl = Global.NUrl + "/latest";
        private const string nUrl = Global.NUrl + "/download/{0}/v2rayN.zip";
        private readonly string v2flyCoreLatestUrl = Global.v2flyCoreUrl + "/latest";
        private const string v2flyCoreUrl = Global.v2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip";
        private readonly string xrayCoreLatestUrl = Global.xrayCoreUrl + "/latest";
        private const string xrayCoreUrl = Global.xrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip";
        private const string geoUrl = "https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/{0}.dat";

        public void CheckUpdateGuiN(Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Empty;

              DownloadHandle downloadHandle = null;
            if (downloadHandle == null)
            {
                downloadHandle = new DownloadHandle();

                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        _updateFunc(false, UIRes.I18N("MsgDownloadV2rayCoreSuccessfully"));

                        try
                        {
                            string fileName = Utils.GetPath(Utils.GetDownloadFileName(url));
                            fileName = Utils.UrlEncode(fileName);
                            Process process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "v2rayUpgrade.exe",
                                    Arguments = "\"" + fileName + "\"",
                                    WorkingDirectory = Utils.StartupPath()
                                }
                            };
                            process.Start();
                            if (process.Id > 0)
                            {
                                _updateFunc(true, "");
                            }
                        }
                        catch (Exception ex)
                        {
                            _updateFunc(false, ex.Message);
                        }
                    }
                    else
                    {
                        _updateFunc(false, args.Msg);
                    }
                };
                downloadHandle.Error += (sender2, args) =>
                {
                    _updateFunc(false, args.GetException().Message);
                };
            }
            AbsoluteCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(UIRes.I18N("MsgParsingSuccessfully"), "v2rayN"));

                    url = args.Msg;
                    askToDownload(downloadHandle, url, true);
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(UIRes.I18N("MsgStartUpdating"), "v2rayN"));
            CheckUpdateAsync("v2rayN");
        }


        public void CheckUpdateCore(string type, Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Empty;

            DownloadHandle downloadHandle = null;
            if (downloadHandle == null)
            {
                downloadHandle = new DownloadHandle();
                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        _updateFunc(false, UIRes.I18N("MsgDownloadV2rayCoreSuccessfully"));
                        _updateFunc(false, UIRes.I18N("MsgUnpacking"));

                        try
                        {
                            _updateFunc(true, url);
                        }
                        catch (Exception ex)
                        {
                            _updateFunc(false, ex.Message);
                        }
                    }
                    else
                    {
                        _updateFunc(false, args.Msg);
                    }
                };
                downloadHandle.Error += (sender2, args) =>
                {
                    _updateFunc(true, args.GetException().Message);
                };
            }

            AbsoluteCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(UIRes.I18N("MsgParsingSuccessfully"), "Core"));
                    url = args.Msg;
                    askToDownload(downloadHandle, url, true);
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(UIRes.I18N("MsgStartUpdating"), "Core"));
            CheckUpdateAsync(type);
        }


        public void UpdateSubscriptionProcess(Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;

            _updateFunc(false, UIRes.I18N("MsgUpdateSubscriptionStart"));

            if (config.subItem == null || config.subItem.Count <= 0)
            {
                _updateFunc(false, UIRes.I18N("MsgNoValidSubscription"));
                return;
            }

            for (int k = 1; k <= config.subItem.Count; k++)
            {
                string id = config.subItem[k - 1].id.Trim();
                string url = config.subItem[k - 1].url.Trim();
                string hashCode = $"{k}->";
                if (config.subItem[k - 1].enabled == false)
                {
                    continue;
                }
                if (Utils.IsNullOrEmpty(id) || Utils.IsNullOrEmpty(url))
                {
                    _updateFunc(false, $"{hashCode}{UIRes.I18N("MsgNoValidSubscription")}");
                    continue;
                }

                DownloadHandle downloadHandle3 = new DownloadHandle();
                downloadHandle3.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        _updateFunc(false, $"{hashCode}{UIRes.I18N("MsgGetSubscriptionSuccessfully")}");
                        string result = Utils.Base64Decode(args.Msg);
                        if (Utils.IsNullOrEmpty(result))
                        {
                            _updateFunc(false, $"{hashCode}{UIRes.I18N("MsgSubscriptionDecodingFailed")}");
                            return;
                        }

                        ConfigHandler.RemoveServerViaSubid(ref config, id);
                        _updateFunc(false, $"{hashCode}{UIRes.I18N("MsgClearSubscription")}");
                        //  RefreshServers();
                        int ret = MainFormHandler.Instance.AddBatchServers(config, result, id);
                        if (ret > 0)
                        {
                            // RefreshServers();
                        }
                        else
                        {
                            _updateFunc(false, $"{hashCode}{UIRes.I18N("MsgFailedImportSubscription")}");
                        }
                        _updateFunc(true, $"{hashCode}{UIRes.I18N("MsgUpdateSubscriptionEnd")}");
                    }
                    else
                    {
                        _updateFunc(false, args.Msg);
                    }
                };
                downloadHandle3.Error += (sender2, args) =>
                {
                    _updateFunc(false, args.GetException().Message);
                };

                downloadHandle3.WebDownloadString(url);
                _updateFunc(false, $"{hashCode}{UIRes.I18N("MsgStartGettingSubscriptions")}");
            }

        }


        public void UpdateGeoFile(string geoName, Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Format(geoUrl, geoName);

            DownloadHandle downloadHandle = null;
            if (downloadHandle == null)
            {
                downloadHandle = new DownloadHandle();

                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        _updateFunc(false, string.Format(UIRes.I18N("MsgDownloadGeoFileSuccessfully"), geoName));

                        try
                        {
                            string fileName = Utils.GetPath(Utils.GetDownloadFileName(url));
                            if (File.Exists(fileName))
                            {
                                string targetPath = Utils.GetPath($"{geoName}.dat");
                                if (File.Exists(targetPath))
                                {
                                    File.Delete(targetPath);
                                }
                                File.Move(fileName, targetPath);
                                _updateFunc(true, "");
                            }
                        }
                        catch (Exception ex)
                        {
                            _updateFunc(false, ex.Message);
                        }
                    }
                    else
                    {
                        _updateFunc(false, args.Msg);
                    }
                };
                downloadHandle.Error += (sender2, args) =>
                {
                    _updateFunc(false, args.GetException().Message);
                };
            }

            askToDownload(downloadHandle, url, false);
        }

        #region private

        private async void CheckUpdateAsync(string type)
        {
            try
            {
                Utils.SetSecurityProtocol();
                WebRequestHandler webRequestHandler = new WebRequestHandler
                {
                    AllowAutoRedirect = false
                };
                if (httpProxyTest() > 0)
                {
                    int httpPort = _config.GetLocalPort(Global.InboundHttp);
                    WebProxy webProxy = new WebProxy(Global.Loopback, httpPort);
                    webRequestHandler.Proxy = webProxy;
                }
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
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                _updateFunc(false, ex.Message);
            }
        }

        /// <summary>
        /// 获取V2RayCore版本
        /// </summary>
        private string getCoreVersion(string type)
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
                _updateFunc(false, ex.Message);
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
                _updateFunc(false, ex.Message);
            }
        }

        private void askToDownload(DownloadHandle downloadHandle, string url, bool blAsk)
        {
            bool blDownload = false;
            if (blAsk)
            {
                if (UI.ShowYesNo(string.Format(UIRes.I18N("DownloadYesNo"), url)) == DialogResult.Yes)
                {
                    blDownload = true;
                }
            }
            else
            {
                blDownload = true;
            }
            if (blDownload)
            {
                if (httpProxyTest() > 0)
                {
                    int httpPort = _config.GetLocalPort(Global.InboundHttp);
                    WebProxy webProxy = new WebProxy(Global.Loopback, httpPort);
                    downloadHandle.DownloadFileAsync(url, webProxy, 600);
                }
                else
                {
                    downloadHandle.DownloadFileAsync(url, null, 600);
                }
            }
        }

        private int httpProxyTest()
        {
            SpeedtestHandler statistics = new SpeedtestHandler(ref _config);
            return statistics.RunAvailabilityCheck();
        }
        #endregion
    }
}
