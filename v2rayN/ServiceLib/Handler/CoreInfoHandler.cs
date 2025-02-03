namespace ServiceLib.Handler
{
    public sealed class CoreInfoHandler
    {
        private static readonly Lazy<CoreInfoHandler> _instance = new(() => new());
        private List<CoreInfo>? _coreInfo;
        public static CoreInfoHandler Instance => _instance.Value;

        public CoreInfoHandler()
        {
            InitCoreInfo();
        }

        public CoreInfo? GetCoreInfo(ECoreType coreType)
        {
            if (_coreInfo == null)
            {
                InitCoreInfo();
            }
            return _coreInfo?.FirstOrDefault(t => t.CoreType == coreType);
        }

        public List<CoreInfo> GetCoreInfo()
        {
            if (_coreInfo == null)
            {
                InitCoreInfo();
            }
            return _coreInfo ?? [];
        }

        public string GetCoreExecFile(CoreInfo? coreInfo, out string msg)
        {
            var fileName = string.Empty;
            msg = string.Empty;
            foreach (var name in coreInfo?.CoreExes)
            {
                var vName = Utils.GetBinPath(Utils.GetExeName(name), coreInfo.CoreType.ToString());
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (fileName.IsNullOrEmpty())
            {
                msg = string.Format(ResUI.NotFoundCore, Utils.GetBinPath("", coreInfo?.CoreType.ToString()), coreInfo?.CoreExes?.LastOrDefault(), coreInfo?.Url);
                Logging.SaveLog(msg);
            }
            return fileName;
        }

        private void InitCoreInfo()
        {
            _coreInfo =
            [
                new CoreInfo
                {
                    CoreType = ECoreType.v2rayN,
                    Url = Global.NUrl,
                    ReleaseApiUrl = Global.NUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                    DownloadUrlWin64 = Global.NUrl + "/download/{0}/v2rayN-windows-64.zip",
                    DownloadUrlWinArm64 = Global.NUrl + "/download/{0}/v2rayN-windows-arm64.zip",
                    DownloadUrlLinux64 = Global.NUrl + "/download/{0}/v2rayN-linux-64.zip",
                    DownloadUrlLinuxArm64 = Global.NUrl + "/download/{0}/v2rayN-linux-arm64.zip",
                    DownloadUrlOSX64 = Global.NUrl + "/download/{0}/v2rayN-macos-64.zip",
                    DownloadUrlOSXArm64 = Global.NUrl + "/download/{0}/v2rayN-macos-arm64.zip",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.v2fly,
                    CoreExes = ["wv2ray", "v2ray"],
                    Arguments = "{0}",
                    Url = Global.V2flyCoreUrl,
                    ReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                    Match = "V2Ray",
                    VersionArg = "-version",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.v2fly_v5,
                    CoreExes = ["v2ray"],
                    Arguments = "run -c {0} -format jsonv5",
                    Url = Global.V2flyCoreUrl,
                    ReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                    Match = "V2Ray",
                    VersionArg = "version",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.Xray,
                    CoreExes = ["wxray","xray"],
                    Arguments = "run -c {0}",
                    Url = Global.XrayCoreUrl,
                    ReleaseApiUrl = Global.XrayCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                    DownloadUrlWin64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-64.zip",
                    DownloadUrlWinArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-arm64-v8a.zip",
                    DownloadUrlLinux64 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-64.zip",
                    DownloadUrlLinuxArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-arm64-v8a.zip",
                    DownloadUrlOSX64 = Global.XrayCoreUrl + "/download/{0}/Xray-macos-64.zip",
                    DownloadUrlOSXArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-macos-arm64-v8a.zip",
                    Match = "Xray",
                    VersionArg = "-version",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.mihomo,
                    CoreExes = ["mihomo-windows-amd64-compatible", "mihomo-windows-amd64", "mihomo-linux-amd64", "clash", "mihomo"],
                    Arguments = "-f {0}" + PortableMode(),
                    Url = Global.MihomoCoreUrl,
                    ReleaseApiUrl = Global.MihomoCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                    DownloadUrlWin64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-amd64-compatible-{0}.zip",
                    DownloadUrlWinArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-arm64-{0}.zip",
                    DownloadUrlLinux64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-amd64-compatible-{0}.gz",
                    DownloadUrlLinuxArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-arm64-{0}.gz",
                    DownloadUrlOSX64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-darwin-amd64-compatible-{0}.gz",
                    DownloadUrlOSXArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-darwin-arm64-{0}.gz",
                    Match = "Mihomo",
                    VersionArg = "-v",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.hysteria,
                    CoreExes = ["hysteria-windows-amd64", "hysteria"],
                    Arguments = "-c {0}",
                    Url = Global.HysteriaCoreUrl,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.naiveproxy,
                    CoreExes = [ "naive", "naiveproxy"],
                    Arguments = "{0}",
                    Url = Global.NaiveproxyCoreUrl,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.tuic,
                    CoreExes = ["tuic-client", "tuic"],
                    Arguments = "-c {0}",
                    Url = Global.TuicCoreUrl,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.sing_box,
                    CoreExes = ["sing-box-client", "sing-box"],
                    Arguments = "run -c {0} --disable-color",
                    Url = Global.SingboxCoreUrl,

                    ReleaseApiUrl = Global.SingboxCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                    DownloadUrlWin64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-amd64.zip",
                    DownloadUrlWinArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-arm64.zip",
                    DownloadUrlLinux64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-amd64.tar.gz",
                    DownloadUrlLinuxArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-arm64.tar.gz",
                    DownloadUrlOSX64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-darwin-amd64.tar.gz",
                    DownloadUrlOSXArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-darwin-arm64.tar.gz",
                    Match = "sing-box",
                    VersionArg = "version",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.juicity,
                    CoreExes = ["juicity-client", "juicity"],
                    Arguments = "run -c {0}",
                    Url = Global.JuicityCoreUrl
                },

                new CoreInfo
                {
                    CoreType = ECoreType.hysteria2,
                    CoreExes = ["hysteria-windows-amd64", "hysteria-linux-amd64", "hysteria"],
                    Arguments = "-c {0}",
                    Url = Global.HysteriaCoreUrl,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.brook,
                    CoreExes = ["brook_windows_amd64", "brook_linux_amd64", "brook"],
                    Arguments = " {0}",
                    Url = Global.BrookCoreUrl,
                    AbsolutePath = true,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.overtls,
                    CoreExes = [ "overtls-bin", "overtls"],
                    Arguments = "-r client -c {0}",
                    Url = Global.OvertlsCoreUrl,
                    AbsolutePath = false,
                }

            ];
        }

        private string PortableMode()
        {
            return $" -d {Utils.GetBinPath("").AppendQuotes()}";
        }
    }
}
