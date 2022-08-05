using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Resx;

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
                Success = success;
                Msg = msg;
            }
        }

        public void CheckUpdateGuiN(Config config, Action<bool, string> update, bool preRelease)
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
                        _updateFunc(false, ResUI.MsgDownloadV2rayCoreSuccessfully);

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
                    _updateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, "v2rayN"));

                    url = args.Msg;
                    askToDownload(downloadHandle, url, true);
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(ResUI.MsgStartUpdating, "v2rayN"));
            CheckUpdateAsync(ECoreType.v2rayN, preRelease);
        }


        public void CheckUpdateCore(ECoreType type, Config config, Action<bool, string> update, bool preRelease)
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
                        _updateFunc(false, ResUI.MsgDownloadV2rayCoreSuccessfully);
                        _updateFunc(false, ResUI.MsgUnpacking);

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
                    _updateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, "Core"));
                    url = args.Msg;
                    askToDownload(downloadHandle, url, true);
                }
                else
                {
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(ResUI.MsgStartUpdating, "Core"));
            CheckUpdateAsync(type, preRelease);
        }


        public void UpdateSubscriptionProcess(Config config, bool blProxy, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;

            _updateFunc(false, ResUI.MsgUpdateSubscriptionStart);

            if (config.subItem == null || config.subItem.Count <= 0)
            {
                _updateFunc(false, ResUI.MsgNoValidSubscription);
                return;
            }

            Task.Run(async () =>
            {
                //Turn off system proxy
                bool bSysProxyType = false;
                if (!blProxy && config.sysProxyType == ESysProxyType.ForcedChange)
                {
                    bSysProxyType = true;
                    config.sysProxyType = ESysProxyType.ForcedClear;
                    SysProxyHandle.UpdateSysProxy(config, false);
                    Thread.Sleep(3000);
                }

                foreach (var item in config.subItem)
                {
                    if (item.enabled == false)
                    {
                        continue;
                    }
                    string id = item.id.TrimEx();
                    string url = item.url.TrimEx();
                    string userAgent = item.userAgent.TrimEx();
                    string groupId = item.groupId.TrimEx();
                    string hashCode = $"{item.remarks}->";
                    if (Utils.IsNullOrEmpty(id) || Utils.IsNullOrEmpty(url))
                    {
                        //_updateFunc(false, $"{hashCode}{ResUI.MsgNoValidSubscription}");
                        continue;
                    }

                    var downloadHandle = new DownloadHandle();
                    downloadHandle.Error += (sender2, args) =>
                    {
                        _updateFunc(false, $"{hashCode}{args.GetException().Message}");
                    };

                    _updateFunc(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");
                    var result = await downloadHandle.DownloadStringAsync(url, blProxy, userAgent);
                    if (blProxy && Utils.IsNullOrEmpty(result))
                    {
                        result = await downloadHandle.DownloadStringAsync(url, false, userAgent);
                    }

                    if (Utils.IsNullOrEmpty(result))
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
                    }
                    else
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");
                        if (result.Length < 99)
                        {
                            _updateFunc(false, $"{hashCode}{result}");
                        }

                        int ret = ConfigHandler.AddBatchServers(ref config, result, id, groupId);
                        _updateFunc(false,
                            ret > 0
                                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");
                    }
                    _updateFunc(false, "-------------------------------------------------------");
                }
                //restore system proxy
                if (bSysProxyType)
                {
                    config.sysProxyType = ESysProxyType.ForcedChange;
                    SysProxyHandle.UpdateSysProxy(config, false);
                }
                _updateFunc(true, $"{ResUI.MsgUpdateSubscriptionEnd}");

            });
        }


        public void UpdateGeoFile(string geoName, Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Format(Global.geoUrl, geoName);

            DownloadHandle downloadHandle = null;
            if (downloadHandle == null)
            {
                downloadHandle = new DownloadHandle();

                downloadHandle.UpdateCompleted += (sender2, args) =>
                {
                    if (args.Success)
                    {
                        _updateFunc(false, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, geoName));

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
                                //_updateFunc(true, "");
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

        public void RunAvailabilityCheck(Action<bool, string> update)
        {
            Task.Run(() =>
            {
                var time = (new DownloadHandle()).RunAvailabilityCheck(null);

                update(false, string.Format(ResUI.TestMeOutput, time));
            });
        }

        #region private

        private async void CheckUpdateAsync(ECoreType type, bool preRelease)
        {
            try
            {
                var coreInfo = LazyConfig.Instance.GetCoreInfo(type);
                string url = coreInfo.coreReleaseApiUrl;

                var result = await (new DownloadHandle()).DownloadStringAsync(url, true, "");
                if (!Utils.IsNullOrEmpty(result))
                {
                    responseHandler(type, result, preRelease);
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
        private string getCoreVersion(ECoreType type)
        {
            try
            {

                var coreInfo = LazyConfig.Instance.GetCoreInfo(type);
                string filePath = string.Empty;
                foreach (string name in coreInfo.coreExes)
                {
                    string vName = $"{name}.exe";
                    vName = Utils.GetPath(vName);
                    if (File.Exists(vName))
                    {
                        filePath = vName;
                        break;
                    }
                }

                if (!File.Exists(filePath))
                {
                    string msg = string.Format(ResUI.NotFoundCore, @"");
                    //ShowMsg(true, msg);
                    return "";
                }

                Process p = new Process();
                p.StartInfo.FileName = filePath;
                p.StartInfo.Arguments = coreInfo.versionArg;
                p.StartInfo.WorkingDirectory = Utils.StartupPath();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.Start();
                p.WaitForExit(5000);
                string echo = p.StandardOutput.ReadToEnd();
                string version = string.Empty;
                switch (type)
                {
                    case ECoreType.v2fly:
                    case ECoreType.SagerNet:
                    case ECoreType.Xray:
                        version = Regex.Match(echo, $"{coreInfo.match} ([0-9.]+) \\(").Groups[1].Value;
                        break;
                    case ECoreType.clash:
                    case ECoreType.clash_meta:
                        version = Regex.Match(echo, $"v[0-9.]+").Groups[0].Value;
                        break;
                }
                return version;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                _updateFunc(false, ex.Message);
                return "";
            }
        }
        private void responseHandler(ECoreType type, string gitHubReleaseApi, bool preRelease)
        {
            try
            {
                var gitHubReleases =  Utils.FromJson<List<GitHubRelease>>(gitHubReleaseApi);                 
                string version;
                if (preRelease)
                {
                    version = gitHubReleases!.First().TagName;
                }
                else
                {
                    version = gitHubReleases!.First(r => r.Prerelease == false).TagName;
                }
                var coreInfo = LazyConfig.Instance.GetCoreInfo(type);

                string curVersion;
                string message;
                string url;
                switch (type)
                {
                    case ECoreType.v2fly:
                    case ECoreType.SagerNet:
                    case ECoreType.Xray:
                        {
                            curVersion = "v" + getCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, curVersion);
                            string osBit = Environment.Is64BitProcess ? "64" : "32";
                            url = string.Format(coreInfo.coreDownloadUrl64, version, osBit);
                            break;
                        }
                    case ECoreType.clash:
                    case ECoreType.clash_meta:
                        {
                            curVersion = getCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, curVersion);
                            if (Environment.Is64BitProcess)
                            {
                                url = string.Format(coreInfo.coreDownloadUrl64, version);
                            }
                            else
                            {
                                url = string.Format(coreInfo.coreDownloadUrl32, version);
                            }
                            break;
                        }
                    case ECoreType.v2rayN:
                        {
                            curVersion = FileVersionInfo.GetVersionInfo(Utils.GetExePath()).FileVersion.ToString();
                            message = string.Format(ResUI.IsLatestN, curVersion);
                            url = string.Format(coreInfo.coreDownloadUrl64, version);
                            break;
                        }
                    default:
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
                if (UI.ShowYesNo(string.Format(ResUI.DownloadYesNo, url)) == DialogResult.Yes)
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
                downloadHandle.DownloadFileAsync(url, true, 600);
            }
        }
        #endregion
    }
}
