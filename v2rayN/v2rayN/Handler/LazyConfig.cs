using System.Runtime.Intrinsics.X86;
using v2rayN.Model;

namespace v2rayN.Handler
{
    public sealed class LazyConfig
    {
        private static readonly Lazy<LazyConfig> _instance = new(() => new());
        private Config _config;
        private List<CoreInfo> coreInfo;

        public static LazyConfig Instance => _instance.Value;

        private int? _statePort;

        public int StatePort
        {
            get
            {
                if (_statePort is null)
                {
                    _statePort = Utils.GetFreePort(GetLocalPort(EInboundProtocol.api));
                }

                return _statePort.Value;
            }
        }

        private Job _processJob = new();

        public LazyConfig()
        {
            SQLiteHelper.Instance.CreateTable<SubItem>();
            SQLiteHelper.Instance.CreateTable<ProfileItem>();
            SQLiteHelper.Instance.CreateTable<ServerStatItem>();
            SQLiteHelper.Instance.CreateTable<RoutingItem>();
            SQLiteHelper.Instance.CreateTable<ProfileExItem>();
            SQLiteHelper.Instance.CreateTable<DNSItem>();
        }

        #region Config

        public void SetConfig(Config config)
        {
            _config = config;
        }

        public Config GetConfig()
        {
            return _config;
        }

        public int GetLocalPort(EInboundProtocol protocol)
        {
            var localPort = _config.inbound.FirstOrDefault(t => t.protocol == nameof(EInboundProtocol.socks))?.localPort ?? 10808;
            return localPort + (int)protocol;
        }

        public void AddProcess(IntPtr processHandle)
        {
            _processJob.AddProcess(processHandle);
        }

        #endregion Config

        #region SqliteHelper

        public List<SubItem> SubItems()
        {
            return SQLiteHelper.Instance.Table<SubItem>().ToList();
        }

        public SubItem GetSubItem(string subid)
        {
            return SQLiteHelper.Instance.Table<SubItem>().FirstOrDefault(t => t.id == subid);
        }

        public List<ProfileItem> ProfileItems(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().ToList();
            }
            else
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid).ToList();
            }
        }

        public List<string> ProfileItemIndexes(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().Select(t => t.indexId).ToList();
            }
            else
            {
                return SQLiteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid).Select(t => t.indexId).ToList();
            }
        }

        public List<ProfileItemModel> ProfileItems(string subid, string filter)
        {
            var sql = @$"select a.*
                           ,b.remarks subRemarks
                        from ProfileItem a
                        left join SubItem b on a.subid = b.id
                        where 1=1 ";
            if (!Utils.IsNullOrEmpty(subid))
            {
                sql += $" and a.subid = '{subid}'";
            }
            if (!Utils.IsNullOrEmpty(filter))
            {
                if (filter.Contains('\''))
                {
                    filter = filter.Replace("'", "");
                }
                sql += String.Format(" and (a.remarks like '%{0}%' or a.address like '%{0}%') ", filter);
            }

            return SQLiteHelper.Instance.Query<ProfileItemModel>(sql).ToList();
        }

        public ProfileItem? GetProfileItem(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return null;
            }
            return SQLiteHelper.Instance.Table<ProfileItem>().FirstOrDefault(it => it.indexId == indexId);
        }

        public ProfileItem? GetProfileItemViaRemarks(string remarks)
        {
            if (Utils.IsNullOrEmpty(remarks))
            {
                return null;
            }
            return SQLiteHelper.Instance.Table<ProfileItem>().FirstOrDefault(it => it.remarks == remarks);
        }

        public List<RoutingItem> RoutingItems()
        {
            return SQLiteHelper.Instance.Table<RoutingItem>().Where(it => it.locked == false).OrderBy(t => t.sort).ToList();
        }

        public RoutingItem GetRoutingItem(string id)
        {
            return SQLiteHelper.Instance.Table<RoutingItem>().FirstOrDefault(it => it.locked == false && it.id == id);
        }

        public List<DNSItem> DNSItems()
        {
            return SQLiteHelper.Instance.Table<DNSItem>().ToList();
        }

        public DNSItem GetDNSItem(ECoreType eCoreType)
        {
            return SQLiteHelper.Instance.Table<DNSItem>().FirstOrDefault(it => it.coreType == eCoreType);
        }

        #endregion SqliteHelper

        #region Core Type

        public List<string> GetShadowsocksSecurities(ProfileItem profileItem)
        {
            var coreType = GetCoreType(profileItem, EConfigType.Shadowsocks);
            switch (coreType)
            {
                case ECoreType.v2fly:
                    return Global.SsSecurities;

                case ECoreType.Xray:
                    return Global.SsSecuritiesInXray;

                case ECoreType.sing_box:
                    return Global.SsSecuritiesInSingbox;
            }
            return Global.SsSecuritiesInSagerNet;
        }

        public ECoreType GetCoreType(ProfileItem profileItem, EConfigType eConfigType)
        {
            if (profileItem?.coreType != null)
            {
                return (ECoreType)profileItem.coreType;
            }

            if (_config.coreTypeItem == null)
            {
                return ECoreType.Xray;
            }
            var item = _config.coreTypeItem.FirstOrDefault(it => it.configType == eConfigType);
            if (item == null)
            {
                return ECoreType.Xray;
            }
            return item.coreType;
        }

        public CoreInfo? GetCoreInfo(ECoreType coreType)
        {
            if (coreInfo == null)
            {
                InitCoreInfo();
            }
            return coreInfo?.FirstOrDefault(t => t.coreType == coreType);
        }

        public List<CoreInfo> GetCoreInfo()
        {
            if (coreInfo == null)
            {
                InitCoreInfo();
            }
            return coreInfo!;
        }

        private void InitCoreInfo()
        {
            coreInfo = new(16);

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.v2rayN,
                coreUrl = Global.NUrl,
                coreReleaseApiUrl = Global.NUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.NUrl + "/download/{0}/v2rayN-32.zip",
                coreDownloadUrl64 = Global.NUrl + "/download/{0}/v2rayN.zip",
                coreDownloadUrlArm64 = Global.NUrl + "/download/{0}/v2rayN-arm64.zip"
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.v2fly,
                coreExes = new List<string> { "wv2ray", "v2ray" },
                arguments = "",
                coreUrl = Global.V2flyCoreUrl,
                coreReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.V2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                coreDownloadUrl64 = Global.V2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                coreDownloadUrlArm64 = Global.V2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                match = "V2Ray",
                versionArg = "-version",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.SagerNet,
                coreExes = new List<string> { "SagerNet", "v2ray" },
                arguments = "run",
                coreUrl = Global.SagerNetCoreUrl,
                coreReleaseApiUrl = Global.SagerNetCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.SagerNetCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                coreDownloadUrl64 = Global.SagerNetCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                coreDownloadUrlArm64 = Global.SagerNetCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                match = "V2Ray",
                versionArg = "version",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.v2fly_v5,
                coreExes = new List<string> { "v2ray" },
                arguments = "run -c config.json -format jsonv5",
                coreUrl = Global.V2flyCoreUrl,
                coreReleaseApiUrl = Global.V2flyCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.V2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                coreDownloadUrl64 = Global.V2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                coreDownloadUrlArm64 = Global.V2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                match = "V2Ray",
                versionArg = "version",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.Xray,
                coreExes = new List<string> { "xray", "wxray" },
                arguments = "run {0}",
                coreUrl = Global.XrayCoreUrl,
                coreReleaseApiUrl = Global.XrayCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip",
                coreDownloadUrl64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip",
                coreDownloadUrlArm64 = Global.XrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip",
                match = "Xray",
                versionArg = "-version",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.clash,
                coreExes = new List<string> { "clash-windows-amd64-v3", "clash-windows-amd64", "clash-windows-386", "clash" },
                arguments = "-f config.json",
                coreUrl = Global.ClashCoreUrl,
                coreReleaseApiUrl = Global.ClashCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.ClashCoreUrl + "/download/{0}/clash-windows-386-{0}.zip",
                coreDownloadUrl64 = Global.ClashCoreUrl + "/download/{0}/clash-windows-amd64-{0}.zip",
                coreDownloadUrlArm64 = Global.ClashCoreUrl + "/download/{0}/clash-windows-arm64-{0}.zip",
                match = "v",
                versionArg = "-v",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.clash_meta,
                coreExes = new List<string> { "Clash.Meta-windows-amd64-compatible", "Clash.Meta-windows-amd64", "Clash.Meta-windows-386", "Clash.Meta", "clash" },
                arguments = "-f config.json",
                coreUrl = Global.ClashMetaCoreUrl,
                coreReleaseApiUrl = Global.ClashMetaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.ClashMetaCoreUrl + "/download/{0}/Clash.Meta-windows-386-{0}.zip",
                coreDownloadUrl64 = Global.ClashMetaCoreUrl + "/download/{0}/Clash.Meta-windows-amd64-compatible-{0}.zip",
                coreDownloadUrlArm64 = Global.ClashMetaCoreUrl + "/download/{0}/Clash.Meta-windows-arm64-{0}.zip",
                match = "v",
                versionArg = "-v",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.mihomo,
                coreExes = new List<string> { $"mihomo-windows-amd64{(Avx2.X64.IsSupported ? "" : "-compatible")}", "mihomo-windows-amd64-compatible", "mihomo-windows-amd64", "mihomo-windows-386", "mihomo", "clash" },
                arguments = "-f config.json",
                coreUrl = Global.MihomoCoreUrl,
                coreReleaseApiUrl = Global.MihomoCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                match = "Mihomo",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.hysteria,
                coreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                arguments = "",
                coreUrl = Global.HysteriaCoreUrl,
                coreReleaseApiUrl = Global.HysteriaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.HysteriaCoreUrl + "/download/{0}/hysteria-windows-386.exe",
                coreDownloadUrl64 = Global.HysteriaCoreUrl + "/download/{0}/hysteria-windows-amd64.exe",
                coreDownloadUrlArm64 = Global.HysteriaCoreUrl + "/download/{0}/hysteria-windows-arm64.exe",
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.naiveproxy,
                coreExes = new List<string> { "naiveproxy", "naive" },
                arguments = "config.json",
                coreUrl = Global.NaiveproxyCoreUrl,
                redirectInfo = false,
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.tuic,
                coreExes = new List<string> { "tuic-client", "tuic" },
                arguments = "-c config.json",
                coreUrl = Global.TuicCoreUrl,
                redirectInfo = true,
            });

            coreInfo.Add(new CoreInfo
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
                match = "sing-box",
                versionArg = "version",
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.juicity,
                coreExes = new List<string> { "juicity-client", "juicity" },
                arguments = "run -c config.json",
                coreUrl = Global.JuicityCoreUrl
            });

            coreInfo.Add(new CoreInfo
            {
                coreType = ECoreType.hysteria2,
                coreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                arguments = "",
                coreUrl = Global.HysteriaCoreUrl,
                coreReleaseApiUrl = Global.HysteriaCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.HysteriaCoreUrl + "/download/{0}/hysteria-windows-386.exe",
                coreDownloadUrl64 = Global.HysteriaCoreUrl + "/download/{0}/hysteria-windows-amd64.exe",
                coreDownloadUrlArm64 = Global.HysteriaCoreUrl + "/download/{0}/hysteria-windows-arm64.exe",
                redirectInfo = true,
            });
        }

        #endregion Core Type
    }
}