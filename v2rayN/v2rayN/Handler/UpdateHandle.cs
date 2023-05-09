using DynamicData;
using Splat;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using v2rayN.Base;
using v2rayN.Mode;
using v2rayN.Resx;
using v2rayN.Tool;

namespace v2rayN.Handler
{
    internal class UpdateHandle
    {
        private Action<bool, string> _updateFunc;
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

            DownloadHandle downloadHandle = new();
            downloadHandle.UpdateCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, ResUI.MsgDownloadV2rayCoreSuccessfully);

                    try
                    {
                        string fileName = Utils.GetTempPath(Utils.GetDownloadFileName(url));
                        fileName = Utils.UrlEncode(fileName);
                        Process process = new()
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "v2rayUpgrade.exe",
                                Arguments = $"\"{fileName}\"",
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
            AbsoluteCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, "v2rayN"));

                    url = args.Msg;
                    _ = askToDownload(downloadHandle, url, true);
                }
                else
                {
                    Locator.Current.GetService<NoticeHandler>()?.Enqueue(args.Msg);
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

            DownloadHandle downloadHandle = new();
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

            AbsoluteCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, "Core"));
                    url = args.Msg;
                    _ = askToDownload(downloadHandle, url, true);
                }
                else
                {
                    Locator.Current.GetService<NoticeHandler>()?.Enqueue(args.Msg);
                    _updateFunc(false, args.Msg);
                }
            };
            _updateFunc(false, string.Format(ResUI.MsgStartUpdating, "Core"));
            CheckUpdateAsync(type, preRelease);
        }

        public void UpdateSubscriptionProcess(Config config, string subId, bool blProxy, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;

            _updateFunc(false, ResUI.MsgUpdateSubscriptionStart);
            var subItem = LazyConfig.Instance.SubItems();

            if (subItem == null || subItem.Count <= 0)
            {
                _updateFunc(false, ResUI.MsgNoValidSubscription);
                return;
            }

            Task.Run(async () =>
            {
                foreach (var item in subItem)
                {
                    string id = item.id.TrimEx();
                    string url = item.url.TrimEx();
                    string userAgent = item.userAgent.TrimEx();
                    string hashCode = $"{item.remarks}->";
                    if (Utils.IsNullOrEmpty(id) || Utils.IsNullOrEmpty(url) || (!Utils.IsNullOrEmpty(subId) && item.id != subId))
                    {
                        //_updateFunc(false, $"{hashCode}{ResUI.MsgNoValidSubscription}");
                        continue;
                    }
                    if (item.enabled == false)
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgSkipSubscriptionUpdate}");
                        continue;
                    }

                    var downloadHandle = new DownloadHandle();
                    downloadHandle.Error += (sender2, args) =>
                    {
                        _updateFunc(false, $"{hashCode}{args.GetException().Message}");
                    };

                    _updateFunc(false, $"{hashCode}{ResUI.MsgStartGettingSubscriptions}");

                    //one url
                    url = Utils.GetPunycode(url);
                    //convert
                    if (!Utils.IsNullOrEmpty(item.convertTarget))
                    {
                        var subConvertUrl = string.IsNullOrEmpty(config.constItem.subConvertUrl) ? Global.SubConvertUrls.FirstOrDefault() : config.constItem.subConvertUrl;
                        url = string.Format(subConvertUrl!, Utils.UrlEncode(url));
                        if (!url.Contains("target="))
                        {
                            url += string.Format("&target={0}", item.convertTarget);
                        }
                        if (!url.Contains("config="))
                        {
                            url += string.Format("&config={0}", Global.SubConvertConfig.FirstOrDefault());
                        }
                    }
                    var result = await downloadHandle.TryDownloadString(url, blProxy, userAgent);
                    if (blProxy && Utils.IsNullOrEmpty(result))
                    {
                        result = await downloadHandle.TryDownloadString(url, false, userAgent);
                    }

                    //more url
                    if (Utils.IsNullOrEmpty(item.convertTarget) && !Utils.IsNullOrEmpty(item.moreUrl.TrimEx()))
                    {
                        if (!Utils.IsNullOrEmpty(result) && Utils.IsBase64String(result))
                        {
                            result = Utils.Base64Decode(result);
                        }

                        var lstUrl = new List<string>
                        {
                            item.moreUrl.TrimEx().Split(",")
                        };
                        foreach (var it in lstUrl)
                        {
                            var url2 = Utils.GetPunycode(it);
                            if (Utils.IsNullOrEmpty(url2))
                            {
                                continue;
                            }

                            var result2 = await downloadHandle.TryDownloadString(url2, blProxy, userAgent);
                            if (blProxy && Utils.IsNullOrEmpty(result2))
                            {
                                result2 = await downloadHandle.TryDownloadString(url2, false, userAgent);
                            }
                            if (!Utils.IsNullOrEmpty(result2))
                            {
                                if (Utils.IsBase64String(result2))
                                {
                                    result += Utils.Base64Decode(result2);
                                }
                                else
                                {
                                    result += result2;
                                }
                            }
                        }
                    }

                    if (Utils.IsNullOrEmpty(result))
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgSubscriptionDecodingFailed}");
                    }
                    else
                    {
                        _updateFunc(false, $"{hashCode}{ResUI.MsgGetSubscriptionSuccessfully}");
                        if (result!.Length < 99)
                        {
                            _updateFunc(false, $"{hashCode}{result}");
                        }

                        int ret = ConfigHandler.AddBatchServers(ref config, result, id, true);
                        if (ret <= 0)
                        {
                            Utils.SaveLog("FailedImportSubscription");
                            Utils.SaveLog(result);
                        }
                        _updateFunc(false,
                            ret > 0
                                ? $"{hashCode}{ResUI.MsgUpdateSubscriptionEnd}"
                                : $"{hashCode}{ResUI.MsgFailedImportSubscription}");
                    }
                    _updateFunc(false, "-------------------------------------------------------");
                }

                _updateFunc(true, $"{ResUI.MsgUpdateSubscriptionEnd}");
            });
        }

        public void UpdateGeoFileAll(Config config, Action<bool, string> update)
        {
            Task.Run(async () =>
            {
                await UpdateGeoFile("geosite", _config, update);
                await UpdateGeoFile("geoip", _config, update);

                await UpdateGeoFile4Singbox("geosite", _config, false, update);
                await UpdateGeoFile4Singbox("geoip", _config, true, update);
            });
        }

        public void RunAvailabilityCheck(Action<bool, string> update)
        {
            Task.Run(async () =>
            {
                var time = await (new DownloadHandle()).RunAvailabilityCheck(null);

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
        private SemanticVersion getCoreVersion(ECoreType type)
        {
            try
            {
                var coreInfo = LazyConfig.Instance.GetCoreInfo(type);
                string filePath = string.Empty;
                foreach (string name in coreInfo.coreExes)
                {
                    string vName = $"{name}.exe";
                    vName = Utils.GetBinPath(vName, coreInfo.coreType);
                    if (File.Exists(vName))
                    {
                        filePath = vName;
                        break;
                    }
                }

                if (!File.Exists(filePath))
                {
                    string msg = string.Format(ResUI.NotFoundCore, @"", "", "");
                    //ShowMsg(true, msg);
                    return new SemanticVersion("");
                }

                using Process p = new();
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
                    case ECoreType.v2fly_v5:
                        version = Regex.Match(echo, $"{coreInfo.match} ([0-9.]+) \\(").Groups[1].Value;
                        break;

                    case ECoreType.clash:
                    case ECoreType.clash_meta:
                        version = Regex.Match(echo, $"v[0-9.]+").Groups[0].Value;
                        break;

                    case ECoreType.sing_box:
                        version = Regex.Match(echo, $"([0-9.]+)").Groups[1].Value;
                        break;
                }
                return new SemanticVersion(version);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                _updateFunc(false, ex.Message);
                return new SemanticVersion("");
            }
        }

        private void responseHandler(ECoreType type, string gitHubReleaseApi, bool preRelease)
        {
            try
            {
                var gitHubReleases = Utils.FromJson<List<GitHubRelease>>(gitHubReleaseApi);
                SemanticVersion version;
                if (preRelease)
                {
                    version = new SemanticVersion(gitHubReleases!.First().TagName);
                }
                else
                {
                    version = new SemanticVersion(gitHubReleases!.First(r => r.Prerelease == false).TagName);
                }
                var coreInfo = LazyConfig.Instance.GetCoreInfo(type);

                SemanticVersion curVersion;
                string message;
                string url;
                switch (type)
                {
                    case ECoreType.v2fly:
                    case ECoreType.SagerNet:
                    case ECoreType.Xray:
                    case ECoreType.v2fly_v5:
                        {
                            curVersion = getCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, curVersion.ToVersionString("v"));
                            string osBit = "64";
                            switch (RuntimeInformation.ProcessArchitecture)
                            {
                                case Architecture.Arm64:
                                    osBit = "arm64-v8a";
                                    break;

                                case Architecture.X86:
                                    osBit = "32";
                                    break;

                                default:
                                    osBit = "64";
                                    break;
                            }

                            url = string.Format(coreInfo.coreDownloadUrl64, version.ToVersionString("v"), osBit);
                            break;
                        }
                    case ECoreType.clash:
                    case ECoreType.clash_meta:
                        {
                            curVersion = getCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, curVersion);
                            switch (RuntimeInformation.ProcessArchitecture)
                            {
                                case Architecture.Arm64:
                                    url = coreInfo.coreDownloadUrlArm64;
                                    break;

                                case Architecture.X86:
                                    url = coreInfo.coreDownloadUrl32;
                                    break;

                                default:
                                    url = coreInfo.coreDownloadUrl64;
                                    break;
                            }
                            url = string.Format(url, version);
                            break;
                        }
                    case ECoreType.sing_box:
                        {
                            curVersion = getCoreVersion(type);
                            message = string.Format(ResUI.IsLatestCore, curVersion.ToVersionString("v"));
                            switch (RuntimeInformation.ProcessArchitecture)
                            {
                                case Architecture.Arm64:
                                    url = coreInfo.coreDownloadUrlArm64;
                                    break;

                                case Architecture.X86:
                                    url = coreInfo.coreDownloadUrl32;
                                    break;

                                default:
                                    url = coreInfo.coreDownloadUrl64;
                                    break;
                            }
                            url = string.Format(url, version.ToVersionString("v"), version);
                            break;
                        }
                    case ECoreType.v2rayN:
                        {
                            curVersion = new SemanticVersion(FileVersionInfo.GetVersionInfo(Utils.GetExePath()).FileVersion.ToString());
                            message = string.Format(ResUI.IsLatestN, curVersion);
                            switch (RuntimeInformation.ProcessArchitecture)
                            {
                                case Architecture.Arm64:
                                    url = string.Format(coreInfo.coreDownloadUrlArm64, version);
                                    break;

                                case Architecture.X86:
                                    url = string.Format(coreInfo.coreDownloadUrl32, version);
                                    break;

                                default:
                                    url = string.Format(coreInfo.coreDownloadUrl64, version);
                                    break;
                            }
                            break;
                        }
                    default:
                        throw new ArgumentException("Type");
                }

                if (type == ECoreType.v2rayN)
                {
                    decimal.TryParse(curVersion.ToString(), out decimal decCur);
                    decimal.TryParse(version.ToString(), out decimal dec);
                    if (decCur >= dec)
                    {
                        AbsoluteCompleted?.Invoke(this, new ResultEventArgs(false, message));
                        return;
                    }
                }
                if (curVersion >= version)
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

        private async Task askToDownload(DownloadHandle downloadHandle, string url, bool blAsk)
        {
            bool blDownload = false;
            if (blAsk)
            {
                if (UI.ShowYesNo(string.Format(ResUI.DownloadYesNo, url)) == MessageBoxResult.Yes)
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
                await downloadHandle.DownloadFileAsync(url, true, 600);
            }
        }

        private async Task UpdateGeoFile(string geoName, Config config, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Format(Global.geoUrl, geoName);

            DownloadHandle downloadHandle = new();
            downloadHandle.UpdateCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, geoName));

                    try
                    {
                        string fileName = Utils.GetTempPath(Utils.GetDownloadFileName(url));
                        if (File.Exists(fileName))
                        {
                            //Global.coreTypes.ForEach(it =>
                            //{
                            //    string targetPath = Utils.GetBinPath($"{geoName}.dat", (ECoreType)Enum.Parse(typeof(ECoreType), it));
                            //    File.Copy(fileName, targetPath, true);
                            //});
                            string targetPath = Utils.GetBinPath($"{geoName}.dat");
                            File.Copy(fileName, targetPath, true);

                            File.Delete(fileName);
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
            await askToDownload(downloadHandle, url, false);
        }

        private async Task UpdateGeoFile4Singbox(string geoName, Config config, bool needStop, Action<bool, string> update)
        {
            _config = config;
            _updateFunc = update;
            var url = string.Format(Global.singboxGeoUrl, geoName);

            DownloadHandle downloadHandle = new();
            downloadHandle.UpdateCompleted += (sender2, args) =>
            {
                if (args.Success)
                {
                    _updateFunc(false, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, geoName));
                    var coreHandler = Locator.Current.GetService<CoreHandler>();

                    try
                    {
                        if (needStop) coreHandler?.CoreStop();
                        string fileName = Utils.GetTempPath(Utils.GetDownloadFileName(url));
                        if (File.Exists(fileName))
                        {
                            string targetPath = Utils.GetConfigPath($"{geoName}.db");
                            File.Copy(fileName, targetPath, true);

                            File.Delete(fileName);
                        }
                        if (needStop) coreHandler?.LoadCore();
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
            await askToDownload(downloadHandle, url, false);
        }

        #endregion private
    }
}