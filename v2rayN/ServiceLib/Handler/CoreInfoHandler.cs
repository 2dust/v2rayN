using System.Runtime.Intrinsics.X86;

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

        private void InitCoreInfo()
        {
            _coreInfo = [];

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.v2rayN,
                Url = Global.NUrl,
                ReleaseApiUrl = Global.NUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                DownloadUrlWin32 = Global.NUrl + "/download/{0}/v2rayN-windows-32.zip",
                DownloadUrlWin64 = Global.NUrl + "/download/{0}/v2rayN-windows-64.zip",
                DownloadUrlWinArm64 = Global.NUrl + "/download/{0}/v2rayN-windows-arm64.zip",
                DownloadUrlLinux64 = Global.NUrl + "/download/{0}/v2rayN-linux-64.zip",
                DownloadUrlLinuxArm64 = Global.NUrl + "/download/{0}/v2rayN-linux-arm64.zip",
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.v2fly,
                CoreExes = new List<string> { "wv2ray", "v2ray" },
                Arguments = "",
                Url = Global.V2flyCoreUrl,
                ReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                Match = "V2Ray",
                VersionArg = "-version",
                RedirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.v2fly_v5,
                CoreExes = new List<string> { "v2ray" },
                Arguments = "run -c config.json -format jsonv5",
                Url = Global.V2flyCoreUrl,
                ReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                Match = "V2Ray",
                VersionArg = "version",
                RedirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.Xray,
                CoreExes = new List<string> { "xray", "wxray" },
                Arguments = "run {0}",
                Url = Global.XrayCoreUrl,
                ReleaseApiUrl = Global.XrayCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                DownloadUrlWin32 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-32.zip",
                DownloadUrlWin64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-64.zip",
                DownloadUrlWinArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-arm64-v8a.zip",
                DownloadUrlLinux64 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-64.zip",
                DownloadUrlLinuxArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-arm64-v8a.zip",
                Match = "Xray",
                VersionArg = "-version",
                RedirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.mihomo,
                CoreExes = new List<string> { $"mihomo-windows-amd64{(Avx2.X64.IsSupported ? "" : "-compatible")}", "mihomo-windows-amd64-compatible", "mihomo-windows-amd64", "mihomo-windows-386", "mihomo", "clash" },
                Arguments = "-f config.json" + PortableMode(),
                Url = Global.MihomoCoreUrl,
                ReleaseApiUrl = Global.MihomoCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                DownloadUrlWin32 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-386-{0}.zip",
                DownloadUrlWin64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-amd64-compatible-{0}.zip",
                DownloadUrlWinArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-arm64-{0}.zip",
                DownloadUrlLinux64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-amd64-compatible-{0}.gz",
                DownloadUrlLinuxArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-arm64-{0}.gz",
                Match = "Mihomo",
                VersionArg = "-v",
                RedirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.hysteria,
                CoreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                Arguments = "",
                Url = Global.HysteriaCoreUrl,
                ReleaseApiUrl = Global.HysteriaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                RedirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.naiveproxy,
                CoreExes = new List<string> { "naiveproxy", "naive" },
                Arguments = "config.json",
                Url = Global.NaiveproxyCoreUrl,
                RedirectInfo = false,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.tuic,
                CoreExes = new List<string> { "tuic-client", "tuic" },
                Arguments = "-c config.json",
                Url = Global.TuicCoreUrl,
                RedirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.sing_box,
                CoreExes = new List<string> { "sing-box-client", "sing-box" },
                Arguments = "run {0} --disable-color",
                Url = Global.SingboxCoreUrl,
                RedirectInfo = true,
                ReleaseApiUrl = Global.SingboxCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                DownloadUrlWin32 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-386.zip",
                DownloadUrlWin64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-amd64.zip",
                DownloadUrlWinArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-arm64.zip",
                DownloadUrlLinux64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-amd64.tar.gz",
                DownloadUrlLinuxArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-arm64.tar.gz",
                Match = "sing-box",
                VersionArg = "version",
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.juicity,
                CoreExes = new List<string> { "juicity-client", "juicity" },
                Arguments = "run -c config.json",
                Url = Global.JuicityCoreUrl
            });

            _coreInfo.Add(new CoreInfo
            {
                CoreType = ECoreType.hysteria2,
                CoreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                Arguments = "",
                Url = Global.HysteriaCoreUrl,
                ReleaseApiUrl = Global.HysteriaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                RedirectInfo = true,
            });
        }

        private string PortableMode()
        {
            return $" -d \"{Utils.GetBinPath("")}\"";
        }
    }
}