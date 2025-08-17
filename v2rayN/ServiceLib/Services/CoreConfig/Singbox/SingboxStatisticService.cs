namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    private async Task<int> GenExperimental(SingboxConfig singboxConfig)
    {
        //if (_config.guiItem.enableStatistics)
        {
            singboxConfig.experimental ??= new Experimental4Sbox();
            singboxConfig.experimental.clash_api = new Clash_Api4Sbox()
            {
                external_controller = $"{Global.Loopback}:{AppManager.Instance.StatePort2}",
            };
        }

        if (_config.CoreBasicItem.EnableCacheFile4Sbox)
        {
            singboxConfig.experimental ??= new Experimental4Sbox();
            singboxConfig.experimental.cache_file = new CacheFile4Sbox()
            {
                enabled = true,
                path = Utils.GetBinPath("cache.db"),
                store_fakeip = _config.SimpleDNSItem.FakeIP == true
            };
        }

        return await Task.FromResult(0);
    }
}
