namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private void GenExperimental()
    {
        //if (_config.guiItem.enableStatistics)
        {
            _coreConfig.experimental ??= new Experimental4Sbox();
            _coreConfig.experimental.clash_api = new Clash_Api4Sbox()
            {
                external_controller = $"{Global.Loopback}:{AppManager.Instance.StatePort2}",
            };
        }

        if (_config.CoreBasicItem.EnableCacheFile4Sbox)
        {
            _coreConfig.experimental ??= new Experimental4Sbox();
            _coreConfig.experimental.cache_file = new CacheFile4Sbox()
            {
                enabled = true,
                path = Utils.GetBinPath("cache.db"),
                store_fakeip = context.SimpleDnsItem.FakeIP == true
            };
        }
    }
}
