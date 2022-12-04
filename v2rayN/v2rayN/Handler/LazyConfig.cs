﻿using System;
using System.Collections.Generic;
using v2rayN.Mode;
using System.Linq;

namespace v2rayN.Handler
{
    public sealed class LazyConfig
    {
        private static readonly Lazy<LazyConfig> _instance = new Lazy<LazyConfig>(() => new LazyConfig());
        private Config _config;
        private List<CoreInfo> coreInfos;

        public static LazyConfig Instance => _instance.Value;

        public void SetConfig(ref Config config)
        {
            _config = config;
        }
        public Config GetConfig()
        {
            return _config;
        }

        public List<string> GetShadowsocksSecuritys(VmessItem vmessItem)
        {
            if (GetCoreType(vmessItem, EConfigType.Shadowsocks) == ECoreType.v2fly)
            {
                return Global.ssSecuritys;
            }
            if (GetCoreType(vmessItem, EConfigType.Shadowsocks) == ECoreType.Xray)
            {
                return Global.ssSecuritysInXray;
            }

            return Global.ssSecuritysInSagerNet;
        }

        public ECoreType GetCoreType(VmessItem vmessItem, EConfigType eConfigType)
        {
            if (vmessItem != null && vmessItem.coreType != null)
            {
                return (ECoreType)vmessItem.coreType;
            }

            if (_config.coreTypeItem == null)
            {
                return ECoreType.Xray;
            }
            var item = _config.coreTypeItem.FirstOrDefault(it => it.configType == eConfigType);
            return item?.coreType ?? ECoreType.Xray;
        }

        public CoreInfo GetCoreInfo(ECoreType coreType)
        {
            if (coreInfos == null)
            {
                InitCoreInfo();
            }
            return coreInfos.FirstOrDefault(t => t.coreType == coreType);
        }

        private void InitCoreInfo()
        {
            coreInfos = new List<CoreInfo>
            {
                new CoreInfo
                {
                    coreType = ECoreType.v2rayN,
                    coreUrl = Global.NUrl,
                    coreReleaseApiUrl = Global.NUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.NUrl + "/download/{0}/v2rayN.zip",
                    coreDownloadUrl64 = Global.NUrl + "/download/{0}/v2rayN.zip",
                },
                new CoreInfo
                {
                    coreType = ECoreType.v2fly,
                    coreExes = new List<string> { "wv2ray", "v2ray" },
                    arguments = "",
                    coreUrl = Global.v2flyCoreUrl,
                    coreReleaseApiUrl = Global.v2flyCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.v2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                    coreDownloadUrl64 = Global.v2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                    match = "V2Ray",
                    versionArg = "-version",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.SagerNet,
                    coreExes = new List<string> { "SagerNet", "v2ray" },
                    arguments = "run",
                    coreUrl = Global.SagerNetCoreUrl,
                    coreReleaseApiUrl = Global.SagerNetCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.SagerNetCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                    coreDownloadUrl64 = Global.SagerNetCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                    match = "V2Ray",
                    versionArg = "version",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.v2fly_v5,
                    coreExes = new List<string> { "v2ray" },
                    arguments = "run",
                    coreUrl = Global.v2flyCoreUrl,
                    coreReleaseApiUrl = Global.v2flyCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.v2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                    coreDownloadUrl64 = Global.v2flyCoreUrl + "/download/{0}/v2ray-windows-{1}.zip",
                    match = "V2Ray",
                    versionArg = "version",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.Xray,
                    coreExes = new List<string> { "xray" },
                    arguments = "",
                    coreUrl = Global.xrayCoreUrl,
                    coreReleaseApiUrl = Global.xrayCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.xrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip",
                    coreDownloadUrl64 = Global.xrayCoreUrl + "/download/{0}/Xray-windows-{1}.zip",
                    match = "Xray",
                    versionArg = "-version",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.clash,
                    coreExes = new List<string> { "clash-windows-amd64-v3", "clash-windows-amd64", "clash-windows-386", "clash" },
                    arguments = "-f config.json",
                    coreUrl = Global.clashCoreUrl,
                    coreReleaseApiUrl = Global.clashCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.clashCoreUrl + "/download/{0}/clash-windows-386-{0}.zip",
                    coreDownloadUrl64 = Global.clashCoreUrl + "/download/{0}/clash-windows-amd64-{0}.zip",
                    match = "v",
                    versionArg = "-v",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.clash_meta,
                    coreExes = new List<string> { "Clash.Meta-windows-amd64-compatible", "Clash.Meta-windows-amd64", "Clash.Meta-windows-386", "Clash.Meta", "clash" },
                    arguments = "-f config.json",
                    coreUrl = Global.clashMetaCoreUrl,
                    coreReleaseApiUrl = Global.clashMetaCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.clashMetaCoreUrl + "/download/{0}/Clash.Meta-windows-386-{0}.zip",
                    coreDownloadUrl64 = Global.clashMetaCoreUrl + "/download/{0}/Clash.Meta-windows-amd64-compatible-{0}.zip",
                    match = "v",
                    versionArg = "-v",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.hysteria,
                    coreExes = new List<string> { "hysteria-windows-amd64", "hysteria-windows-386", "hysteria" },
                    arguments = "",
                    coreUrl = Global.hysteriaCoreUrl,
                    coreReleaseApiUrl = Global.hysteriaCoreUrl.Replace(@"https://github.com", @"https://api.github.com/repos"),
                    coreDownloadUrl32 = Global.hysteriaCoreUrl + "/download/{0}/hysteria-windows-386.exe",
                    coreDownloadUrl64 = Global.hysteriaCoreUrl + "/download/{0}/hysteria-windows-amd64.exe",
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.naiveproxy,
                    coreExes = new List<string> { "naiveproxy", "naive" },
                    arguments = "config.json",
                    coreUrl = Global.naiveproxyCoreUrl,
                    redirectInfo = false,
                },
                new CoreInfo
                {
                    coreType = ECoreType.tuic,
                    coreExes = new List<string> { "tuic-client", "tuic" },
                    arguments = "-c config.json",
                    coreUrl = Global.tuicCoreUrl,
                    redirectInfo = true,
                },
                new CoreInfo
                {
                    coreType = ECoreType.sing_box,
                    coreExes = new List<string> { "sing-box-client", "sing-box" },
                    arguments = "run",
                    coreUrl = Global.singboxCoreUrl,
                    redirectInfo = true,
                }
            };
        }

    }
}
