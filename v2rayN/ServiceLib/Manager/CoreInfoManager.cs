namespace ServiceLib.Manager;

public sealed class CoreInfoManager
{
    private static readonly Lazy<CoreInfoManager> _instance = new(() => new());
    private List<CoreInfo>? _coreInfo;
    public static CoreInfoManager Instance => _instance.Value;

    public CoreInfoManager()
    {
        InitCoreInfo();
    }

    public CoreInfo? GetCoreInfo(ECoreType coreType)
    {
        if (_coreInfo is null)
        {
            InitCoreInfo();
        }
        return _coreInfo?.FirstOrDefault(t => t.CoreType == coreType);
    }

    public List<CoreInfo> GetCoreInfo()
    {
        if (_coreInfo is null)
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
        var urlN = GetCoreUrl(ECoreType.v2rayN);
        var urlXray = GetCoreUrl(ECoreType.Xray);
        var urlMihomo = GetCoreUrl(ECoreType.mihomo);
        var urlSingbox = GetCoreUrl(ECoreType.sing_box);

        _coreInfo =
        [
            new CoreInfo
                {
                    CoreType = ECoreType.v2rayN,
                    Url = GetCoreUrl(ECoreType.v2rayN),
                    ReleaseApiUrl = urlN.Replace(AppConfig.GithubUrl, AppConfig.GithubApiUrl),
                    DownloadUrlWin64 = urlN + "/download/{0}/v2rayN-windows-64.zip",
                    DownloadUrlWinArm64 = urlN + "/download/{0}/v2rayN-windows-arm64.zip",
                    DownloadUrlLinux64 = urlN + "/download/{0}/v2rayN-linux-64.zip",
                    DownloadUrlLinuxArm64 = urlN + "/download/{0}/v2rayN-linux-arm64.zip",
                    DownloadUrlOSX64 = urlN + "/download/{0}/v2rayN-macos-64.zip",
                    DownloadUrlOSXArm64 = urlN + "/download/{0}/v2rayN-macos-arm64.zip",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.v2fly,
                    CoreExes = ["v2ray"],
                    Arguments = "{0}",
                    Url = GetCoreUrl(ECoreType.v2fly),
                    Match = "V2Ray",
                    VersionArg = "-version",
                    Environment = new Dictionary<string, string?>()
                    {
                        { AppConfig.V2RayLocalAsset, Utils.GetBinPath("") },
                    },
                },

                new CoreInfo
                {
                    CoreType = ECoreType.v2fly_v5,
                    CoreExes = ["v2ray"],
                    Arguments = "run -c {0} -format jsonv5",
                    Url = GetCoreUrl(ECoreType.v2fly_v5),
                    Match = "V2Ray",
                    VersionArg = "version",
                    Environment = new Dictionary<string, string?>()
                    {
                        { AppConfig.V2RayLocalAsset, Utils.GetBinPath("") },
                    },
                },

                new CoreInfo
                {
                    CoreType = ECoreType.Xray,
                    CoreExes = ["xray"],
                    Arguments = "run -c {0}",
                    Url = GetCoreUrl(ECoreType.Xray),
                    ReleaseApiUrl = urlXray.Replace(AppConfig.GithubUrl, AppConfig.GithubApiUrl),
                    DownloadUrlWin64 = urlXray + "/download/{0}/Xray-windows-64.zip",
                    DownloadUrlWinArm64 = urlXray + "/download/{0}/Xray-windows-arm64-v8a.zip",
                    DownloadUrlLinux64 = urlXray + "/download/{0}/Xray-linux-64.zip",
                    DownloadUrlLinuxArm64 = urlXray + "/download/{0}/Xray-linux-arm64-v8a.zip",
                    DownloadUrlOSX64 = urlXray + "/download/{0}/Xray-macos-64.zip",
                    DownloadUrlOSXArm64 = urlXray + "/download/{0}/Xray-macos-arm64-v8a.zip",
                    Match = "Xray",
                    VersionArg = "-version",
                    Environment = new Dictionary<string, string?>()
                    {
                        { AppConfig.XrayLocalAsset, Utils.GetBinPath("") },
                        { AppConfig.XrayLocalCert, Utils.GetBinPath("") },
                    },
                },

                new CoreInfo
                {
                    CoreType = ECoreType.mihomo,
                    CoreExes = GetMihomoCoreExes(),
                    Arguments = "-f {0}" + PortableMode(),
                    Url = GetCoreUrl(ECoreType.mihomo),
                    ReleaseApiUrl = urlMihomo.Replace(AppConfig.GithubUrl, AppConfig.GithubApiUrl),
                    DownloadUrlWin64 = urlMihomo + "/download/{0}/mihomo-windows-amd64-v1-{0}.zip",
                    DownloadUrlWinArm64 = urlMihomo + "/download/{0}/mihomo-windows-arm64-{0}.zip",
                    DownloadUrlLinux64 = urlMihomo + "/download/{0}/mihomo-linux-amd64-v1-{0}.gz",
                    DownloadUrlLinuxArm64 = urlMihomo + "/download/{0}/mihomo-linux-arm64-{0}.gz",
                    DownloadUrlOSX64 = urlMihomo + "/download/{0}/mihomo-darwin-amd64-v1-{0}.gz",
                    DownloadUrlOSXArm64 = urlMihomo + "/download/{0}/mihomo-darwin-arm64-{0}.gz",
                    Match = "Mihomo",
                    VersionArg = "-v",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.hysteria,
                    CoreExes = ["hysteria"],
                    Arguments = "",
                    Url = GetCoreUrl(ECoreType.hysteria),
                },

                new CoreInfo
                {
                    CoreType = ECoreType.naiveproxy,
                    CoreExes = [ "naive", "naiveproxy"],
                    Arguments = "{0}",
                    Url = GetCoreUrl(ECoreType.naiveproxy),
                },

                new CoreInfo
                {
                    CoreType = ECoreType.tuic,
                    CoreExes = ["tuic-client", "tuic"],
                    Arguments = "-c {0}",
                    Url = GetCoreUrl(ECoreType.tuic),
                },

                new CoreInfo
                {
                    CoreType = ECoreType.sing_box,
                    CoreExes = ["sing-box-client", "sing-box"],
                    Arguments = "run -c {0} --disable-color",
                    Url = GetCoreUrl(ECoreType.sing_box),

                    ReleaseApiUrl = urlSingbox.Replace(AppConfig.GithubUrl, AppConfig.GithubApiUrl),
                    DownloadUrlWin64 = urlSingbox + "/download/{0}/sing-box-{1}-windows-amd64.zip",
                    DownloadUrlWinArm64 = urlSingbox + "/download/{0}/sing-box-{1}-windows-arm64.zip",
                    DownloadUrlLinux64 = urlSingbox + "/download/{0}/sing-box-{1}-linux-amd64.tar.gz",
                    DownloadUrlLinuxArm64 = urlSingbox + "/download/{0}/sing-box-{1}-linux-arm64.tar.gz",
                    DownloadUrlOSX64 = urlSingbox + "/download/{0}/sing-box-{1}-darwin-amd64.tar.gz",
                    DownloadUrlOSXArm64 = urlSingbox + "/download/{0}/sing-box-{1}-darwin-arm64.tar.gz",
                    Match = "sing-box",
                    VersionArg = "version",
                },

                new CoreInfo
                {
                    CoreType = ECoreType.juicity,
                    CoreExes = ["juicity-client", "juicity"],
                    Arguments = "run -c {0}",
                    Url = GetCoreUrl(ECoreType.juicity)
                },

                new CoreInfo
                {
                    CoreType = ECoreType.hysteria2,
                    CoreExes = ["hysteria-windows-amd64", "hysteria-linux-amd64", "hysteria"],
                    Arguments = "",
                    Url = GetCoreUrl(ECoreType.hysteria2),
                },

                new CoreInfo
                {
                    CoreType = ECoreType.brook,
                    CoreExes = ["brook_windows_amd64", "brook_linux_amd64", "brook"],
                    Arguments = " {0}",
                    Url = GetCoreUrl(ECoreType.brook),
                    AbsolutePath = true,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.overtls,
                    CoreExes = [ "overtls-bin", "overtls"],
                    Arguments = "-r client -c {0}",
                    Url =  GetCoreUrl(ECoreType.overtls),
                    AbsolutePath = false,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.shadowquic,
                    CoreExes = [ "shadowquic" ],
                    Arguments = "-c {0}",
                    Url =  GetCoreUrl(ECoreType.shadowquic),
                    AbsolutePath = false,
                },

                new CoreInfo
                {
                    CoreType = ECoreType.mieru,
                    CoreExes = [ "mieru" ],
                    Arguments = "run",
                    Url =  GetCoreUrl(ECoreType.mieru),
                    AbsolutePath = false,
                    Environment = new Dictionary<string, string?>()
                    {
                        { "MIERU_CONFIG_JSON_FILE", "{0}" },
                    },
                },
        ];
    }

    private static string PortableMode()
    {
        return $" -d {Utils.GetBinPath("").AppendQuotes()}";
    }

    private static string GetCoreUrl(ECoreType eCoreType)
    {
        return $"{AppConfig.GithubUrl}/{AppConfig.CoreUrls[eCoreType]}/releases";
    }

    private static List<string>? GetMihomoCoreExes()
    {
        var names = new List<string>();

        if (Utils.IsWindows())
        {
            names.Add("mihomo-windows-amd64-v1");
            names.Add("mihomo-windows-amd64-compatible");
            names.Add("mihomo-windows-amd64");
            names.Add("mihomo-windows-arm64");
        }
        else if (Utils.IsLinux())
        {
            names.Add("mihomo-linux-amd64-v1");
            names.Add("mihomo-linux-amd64");
            names.Add("mihomo-linux-arm64");
        }
        else if (Utils.IsMacOS())
        {
            names.Add("mihomo-darwin-amd64-v1");
            names.Add("mihomo-darwin-amd64");
            names.Add("mihomo-darwin-arm64");
        }

        names.Add("clash");
        names.Add("mihomo");

        return names;
    }
}
