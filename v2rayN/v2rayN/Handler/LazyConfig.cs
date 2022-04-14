using System;
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

        public static LazyConfig Instance
        {
            get { return _instance.Value; }
        }
        public void SetConfig(ref Config config)
        {
            _config = config;
        }
        public Config GetConfig()
        {
            return _config;
        }

        public List<string> GetShadowsocksSecuritys()
        {
            if (GetCoreType(null, EConfigType.Shadowsocks) == ECoreType.v2fly)
            {
                return Global.ssSecuritys;
            }

            return Global.ssSecuritysInXray;
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
            if (item == null)
            {
                return ECoreType.Xray;
            }
            return item.coreType;
        }

        public CoreInfo GetCoreInfo(ECoreType coreType)
        {
            if (coreInfos == null)
            {
                InitCoreInfo();
            }
            return coreInfos.Where(t => t.coreType == coreType).FirstOrDefault();
        }

        private void InitCoreInfo()
        {
            coreInfos = new List<CoreInfo>();

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.v2fly,
                coreExes = new List<string> { "wv2ray", "v2ray" },
                arguments = "",
                coreUrl = Global.v2flyCoreUrl,
                match = "V2Ray"
            });

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.Xray,
                coreExes = new List<string> { "xray" },
                arguments = "",
                coreUrl = Global.xrayCoreUrl,
                match = "Xray"
            });

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.clash,
                coreExes = new List<string> { "clash-windows-amd64-v3", "clash-windows-amd64", "clash-windows-386", "clash" },
                arguments = "-f config.json",
                coreUrl = Global.clashCoreUrl
            });

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.clash_meta,
                coreExes = new List<string> { "Clash.Meta-windows-amd64v1", "Clash.Meta-windows-amd64", "Clash.Meta-windows-386", "Clash.Meta", "clash" },
                arguments = "-f config.yaml",
                coreUrl = Global.clashMetaCoreUrl,
                match = "Clash Meta"
            });

            coreInfos.Add(new CoreInfo
            {
                coreType = ECoreType.hysteria,
                coreExes = new List<string> { "hysteria-tun-windows-6.0-amd64", "hysteria-tun-windows-6.0-386", "hysteria" },
                arguments = "",
                coreUrl = Global.hysteriaCoreUrl
            });
        }

    }
}
