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
            return _coreInfo?.FirstOrDefault(t => t.coreType == coreType);
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
                coreType = ECoreType.v2rayN,
                coreUrl = Global.NUrl,
                coreReleaseApiUrl = Global.NUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.NUrl + "/download/{0}/v2rayN-32.zip",
                coreDownloadUrl64 = Global.NUrl + "/download/{0}/v2rayN.zip",
                coreDownloadUrlArm64 = Global.NUrl + "/download/{0}/v2rayN-arm64.zip",
                coreDownloadUrlLinux32 = Global.NUrl + "/download/{0}/v2rayN-linux-32.zip",
                coreDownloadUrlLinux64 = Global.NUrl + "/download/{0}/v2rayN-linux-64.zip",
                coreDownloadUrlLinuxArm64 = Global.NUrl + "/download/{0}/v2rayN-linux-arm64.zip",
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.v2fly,
                coreExes = new List<string> { "wv2ray", "v2ray" },
                arguments = "",
                coreUrl = Global.V2flyCoreUrl,
                coreReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                match = "V2Ray",
                versionArg = "-version",
                redirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.v2fly_v5,
                coreExes = new List<string> { "v2ray" },
                arguments = "run -c config.json -format jsonv5",
                coreUrl = Global.V2flyCoreUrl,
                coreReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                match = "V2Ray",
                versionArg = "version",
                redirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.Xray,
                coreExes = new List<string> { "xray", "wxray" },
                arguments = "run {0}",
                coreUrl = Global.XrayCoreUrl,
                coreReleaseApiUrl = Global.XrayCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-32.zip",
                coreDownloadUrl64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-64.zip",
                coreDownloadUrlArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-arm64-v8a.zip",
                coreDownloadUrlLinux32 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-32.zip",
                coreDownloadUrlLinux64 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-64.zip",
                coreDownloadUrlLinuxArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-linux-arm64-v8a.zip",
                match = "Xray",
                versionArg = "-version",
                redirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.mihomo,
                coreExes = new List<string> { $"mihomo-windows-amd64{(Avx2.X64.IsSupported ? "" : "-compatible")}", "mihomo-windows-amd64-compatible", "mihomo-windows-amd64", "mihomo-windows-386", "mihomo", "clash" },
                arguments = "-f config.json" + PortableMode(),
                coreUrl = Global.MihomoCoreUrl,
                coreReleaseApiUrl = Global.MihomoCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-386-{0}.zip",
                coreDownloadUrl64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-amd64-compatible-{0}.zip",
                coreDownloadUrlArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-windows-arm64-{0}.zip",
                coreDownloadUrlLinux32 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-386-{0}.gz",
                coreDownloadUrlLinux64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-amd64-compatible-{0}.gz",
                coreDownloadUrlLinuxArm64 = Global.MihomoCoreUrl + "/download/{0}/mihomo-linux-arm64-{0}.gz",
                match = "Mihomo",
                versionArg = "-v",
                redirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.hysteria,
                coreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                arguments = "",
                coreUrl = Global.HysteriaCoreUrl,
                coreReleaseApiUrl = Global.HysteriaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                redirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.naiveproxy,
                coreExes = new List<string> { "naiveproxy", "naive" },
                arguments = "config.json",
                coreUrl = Global.NaiveproxyCoreUrl,
                redirectInfo = false,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.tuic,
                coreExes = new List<string> { "tuic-client", "tuic" },
                arguments = "-c config.json",
                coreUrl = Global.TuicCoreUrl,
                redirectInfo = true,
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.sing_box,
                coreExes = new List<string> { "sing-box-client", "sing-box" },
                arguments = "run {0} --disable-color",
                coreUrl = Global.SingboxCoreUrl,
                redirectInfo = true,
                coreReleaseApiUrl = Global.SingboxCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-386.zip",
                coreDownloadUrl64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-amd64.zip",
                coreDownloadUrlArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-windows-arm64.zip",
                coreDownloadUrlLinux32 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-386.tar.gz",
                coreDownloadUrlLinux64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-amd64.tar.gz",
                coreDownloadUrlLinuxArm64 = Global.SingboxCoreUrl + "/download/{0}/sing-box-{1}-linux-arm64.tar.gz",
                match = "sing-box",
                versionArg = "version",
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.juicity,
                coreExes = new List<string> { "juicity-client", "juicity" },
                arguments = "run -c config.json",
                coreUrl = Global.JuicityCoreUrl
            });

            _coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.hysteria2,
                coreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                arguments = "",
                coreUrl = Global.HysteriaCoreUrl,
                coreReleaseApiUrl = Global.HysteriaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                redirectInfo = true,
            });
        }

        private string PortableMode()
        {
            return $" -d \"{Utils.GetBinPath("")}\"";
        }
    }
}