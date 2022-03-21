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
    }
}
