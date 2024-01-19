using v2rayN.Base;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    public sealed class LazyConfig
    {
        private static readonly Lazy<LazyConfig> _instance = new(() => new());
        private Config _config;
        private List<CoreInfo> coreInfos;

        public static LazyConfig Instance => _instance.Value;

        public LazyConfig()
        {
            SqliteHelper.Instance.CreateTable<SubItem>();
            SqliteHelper.Instance.CreateTable<ProfileItem>();
            SqliteHelper.Instance.CreateTable<ServerStatItem>();
            SqliteHelper.Instance.CreateTable<RoutingItem>();
            SqliteHelper.Instance.CreateTable<ProfileExItem>();
            SqliteHelper.Instance.CreateTable<DNSItem>();
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

        public int GetLocalPort(string protocol)
        {
            int localPort = _config.inbound.FirstOrDefault(t => t.protocol == Global.InboundSocks).localPort;
            if (protocol == Global.InboundSocks)
            {
                return localPort;
            }
            else if (protocol == Global.InboundHttp)
            {
                return localPort + 1;
            }
            else if (protocol == Global.InboundSocks2)
            {
                return localPort + 2;
            }
            else if (protocol == Global.InboundHttp2)
            {
                return localPort + 3;
            }
            else if (protocol == ESysProxyType.Pac.ToString())
            {
                return localPort + 4;
            }
            else if (protocol == "speedtest")
            {
                return localPort + 103;
            }
            return localPort;
        }

        public List<SubItem> SubItems()
        {
            return SqliteHelper.Instance.Table<SubItem>().ToList();
        }

        public SubItem GetSubItem(string subid)
        {
            return SqliteHelper.Instance.Table<SubItem>().FirstOrDefault(t => t.id == subid);
        }

        public List<SubItem> SubItemIndexs(string filterreg)
        {
            if (Utils.IsNullOrEmpty(filterreg))
            {
                return SqliteHelper.Instance.Table<SubItem>().ToList();
            }
            else
            {
                var sql = String.Format("SELECT * FROM SubItem WHERE remarks REGEXP '{0}'",filterreg);
                return SqliteHelper.Instance.Query<SubItem>(sql);
            }
        }

        public List<ProfileItem> ProfileItems(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return SqliteHelper.Instance.Table<ProfileItem>().ToList();
            }
            else
            {
                return SqliteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid).ToList();
            }
        }

        public List<ProfileItem> ProfileItemsAutoSwitch()
        {
                return SqliteHelper.Instance.Table<ProfileItem>().Where(t => t.autoSwitch == true).ToList();
        }

        public List<string> ProfileItemIndexs(string subid)
        {
            if (Utils.IsNullOrEmpty(subid))
            {
                return SqliteHelper.Instance.Table<ProfileItem>().Select(t => t.indexId).ToList();
            }
            else
            {
                return SqliteHelper.Instance.Table<ProfileItem>().Where(t => t.subid == subid).Select(t => t.indexId).ToList();
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

            return SqliteHelper.Instance.Query<ProfileItemModel>(sql).ToList();
        }

        public List<ProfileItemModel> ProfileItemsReg(string subidfilter, string profilefilter)
        {
            var sql = @$"SELECT * FROM ProfileItem";

            if (!Utils.IsNullOrEmpty(profilefilter))
            {
                if (profilefilter[0] != '^' && profilefilter[0] != '.')
                    profilefilter = @".*" + profilefilter;
                if (profilefilter[profilefilter.Length - 1] != '$' && profilefilter[profilefilter.Length - 1] != '*')
                    profilefilter = profilefilter + @".*";

                sql += String.Format(" WHERE remarks REGEXP '{0}'", profilefilter);
            }

            if (!Utils.IsNullOrEmpty(subidfilter))
            {
                //if (subidfilter[0] != '^' && subidfilter[0] != '.')
                //    subidfilter = @".*" + subidfilter;
                //if (subidfilter[subidfilter.Length-1] != '$' && subidfilter[subidfilter.Length - 1] != '*')
                //    subidfilter =subidfilter+@".*";
                if (!Utils.IsNullOrEmpty(profilefilter))
                    sql += String.Format(" AND subid REGEXP '{0}'", subidfilter);
                else
                    sql += String.Format(" WHERE subid REGEXP '{0}'", subidfilter);
            }

            return SqliteHelper.Instance.Query<ProfileItemModel>(sql).ToList();
        }

        public ProfileItem? GetProfileItem(string indexId)
        {
            if (Utils.IsNullOrEmpty(indexId))
            {
                return null;
            }
            return SqliteHelper.Instance.Table<ProfileItem>().FirstOrDefault(it => it.indexId == indexId);
        }

        public ProfileItem? GetProfileItemViaRemarks(string remarks)
        {
            if (Utils.IsNullOrEmpty(remarks))
            {
                return null;
            }
            return SqliteHelper.Instance.Table<ProfileItem>().FirstOrDefault(it => it.remarks == remarks);
        }

        public List<RoutingItem> RoutingItems()
        {
            return SqliteHelper.Instance.Table<RoutingItem>().Where(it => it.locked == false).OrderBy(t => t.sort).ToList();
        }

        public RoutingItem GetRoutingItem(string id)
        {
            return SqliteHelper.Instance.Table<RoutingItem>().FirstOrDefault(it => it.locked == false && it.id == id);
        }

        public List<DNSItem> DNSItems()
        {
            return SqliteHelper.Instance.Table<DNSItem>().ToList();
        }

        public DNSItem GetDNSItem(ECoreType eCoreType)
        {
            return SqliteHelper.Instance.Table<DNSItem>().FirstOrDefault(it => it.coreType == eCoreType);
        }

        #endregion Config

        #region Core Type

        public List<string> GetShadowsocksSecuritys(ProfileItem profileItem)
        {
            if (GetCoreType(profileItem, EConfigType.Shadowsocks) == ECoreType.v2fly)
            {
                return Global.SsSecuritys;
            }
            if (GetCoreType(profileItem, EConfigType.Shadowsocks) == ECoreType.Xray)
            {
                return Global.SsSecuritysInXray;
            }

            return Global.SsSecuritysInSagerNet;
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
            if (coreInfos == null)
            {
                InitCoreInfo();
            }
            return coreInfos!.FirstOrDefault(t => t.coreType == coreType);
        }

        public List<CoreInfo> GetCoreInfos()
        {
            if (coreInfos == null)
            {
                InitCoreInfo();
            }
            return coreInfos!;
        }

        private void InitCoreInfo()
        {
            coreInfos = new(16);

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.v2rayN,
                coreUrl = Global.NUrl,
                coreReleaseApiUrl = Global.NUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                coreDownloadUrl32 = Global.NUrl + "/download/{0}/v2rayN-32.zip",
                coreDownloadUrl64 = Global.NUrl + "/download/{0}/v2rayN.zip",
                coreDownloadUrlArm64 = Global.NUrl + "/download/{0}/v2rayN-arm64.zip"
            });

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.mihomo,
                coreExes = new List<string> { $"mihomo-windows-amd64{(Avx2.X64.IsSupported ? "" : "-compatible")}", "mihomo-windows-amd64-compatible", "mihomo-windows-amd64", "mihomo-windows-386", "mihomo", "clash" },
                arguments = "-f config.yaml",
                coreUrl = Global.MihomoCoreUrl,
                coreReleaseApiUrl = Global.MihomoCoreUrl.Replace(Global.GithubUrl, Global.GithubApiUrl),
                match = "Mihomo",
                redirectInfo = true,
            });

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.naiveproxy,
                coreExes = new List<string> { "naiveproxy", "naive" },
                arguments = "config.json",
                coreUrl = Global.NaiveproxyCoreUrl,
                redirectInfo = false,
            });

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.tuic,
                coreExes = new List<string> { "tuic-client", "tuic" },
                arguments = "-c config.json",
                coreUrl = Global.TuicCoreUrl,
                redirectInfo = true,
            });

            coreInfos.Add(new CoreInfo
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

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.juicity,
                coreExes = new List<string> { "juicity-client", "juicity" },
                arguments = "run -c config.json",
                coreUrl = Global.JuicityCoreUrl
            });

            coreInfos.Add(new CoreInfo
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