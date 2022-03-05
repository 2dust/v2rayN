using System;
using v2rayN.Mode;

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
    }
}
