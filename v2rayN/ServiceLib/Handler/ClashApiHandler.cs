using static ServiceLib.Models.ClashProxies;

namespace ServiceLib.Handler
{
    public sealed class ClashApiHandler
    {
        private static readonly Lazy<ClashApiHandler> instance = new(() => new());
        public static ClashApiHandler Instance => instance.Value;

        private Dictionary<String, ProxiesItem>? _proxies;
        public Dictionary<string, object> ProfileContent { get; set; }

        public void GetClashProxies(Config config, Action<ClashProxies, ClashProviders> updateFunc)
        {
            Task.Run(() => GetClashProxiesAsync(config, updateFunc));
        }

        private async Task GetClashProxiesAsync(Config config, Action<ClashProxies, ClashProviders> updateFunc)
        {
            for (var i = 0; i < 5; i++)
            {
                var url = $"{GetApiUrl()}/proxies";
                var result = await HttpClientHelper.Instance.TryGetAsync(url);
                var clashProxies = JsonUtils.Deserialize<ClashProxies>(result);

                var url2 = $"{GetApiUrl()}/providers/proxies";
                var result2 = await HttpClientHelper.Instance.TryGetAsync(url2);
                var clashProviders = JsonUtils.Deserialize<ClashProviders>(result2);

                if (clashProxies != null || clashProviders != null)
                {
                    _proxies = clashProxies?.proxies;
                    updateFunc?.Invoke(clashProxies, clashProviders);
                    return;
                }
                Task.Delay(5000).Wait();
            }
            updateFunc?.Invoke(null, null);
        }

        public void ClashProxiesDelayTest(bool blAll, List<ClashProxyModel> lstProxy, Action<ClashProxyModel?, string> updateFunc)
        {
            Task.Run(() =>
            {
                if (blAll)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if (_proxies != null)
                        {
                            break;
                        }
                        Task.Delay(5000).Wait();
                    }
                    if (_proxies == null)
                    {
                        return;
                    }
                    lstProxy = new List<ClashProxyModel>();
                    foreach (KeyValuePair<string, ProxiesItem> kv in _proxies)
                    {
                        if (Global.notAllowTestType.Contains(kv.Value.type.ToLower()))
                        {
                            continue;
                        }
                        lstProxy.Add(new ClashProxyModel()
                        {
                            name = kv.Value.name,
                            type = kv.Value.type.ToLower(),
                        });
                    }
                }

                if (lstProxy == null)
                {
                    return;
                }
                var urlBase = $"{GetApiUrl()}/proxies";
                urlBase += @"/{0}/delay?timeout=10000&url=" + AppHandler.Instance.Config.speedTestItem.speedPingTestUrl;

                List<Task> tasks = new List<Task>();
                foreach (var it in lstProxy)
                {
                    if (Global.notAllowTestType.Contains(it.type.ToLower()))
                    {
                        continue;
                    }
                    var name = it.name;
                    var url = string.Format(urlBase, name);
                    tasks.Add(Task.Run(async () =>
                    {
                        var result = await HttpClientHelper.Instance.TryGetAsync(url);
                        updateFunc?.Invoke(it, result);
                    }));
                }
                Task.WaitAll(tasks.ToArray());

                Task.Delay(1000).Wait();
                updateFunc?.Invoke(null, "");
            });
        }

        public List<ProxiesItem>? GetClashProxyGroups()
        {
            try
            {
                var fileContent = ProfileContent;
                if (fileContent is null || fileContent?.ContainsKey("proxy-groups") == false)
                {
                    return null;
                }
                return JsonUtils.Deserialize<List<ProxiesItem>>(JsonUtils.Serialize(fileContent["proxy-groups"]));
            }
            catch (Exception ex)
            {
                Logging.SaveLog("GetClashProxyGroups", ex);
                return null;
            }
        }

        public async void ClashSetActiveProxy(string name, string nameNode)
        {
            try
            {
                var url = $"{GetApiUrl()}/proxies/{name}";
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("name", nameNode);
                await HttpClientHelper.Instance.PutAsync(url, headers);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public void ClashConfigUpdate(Dictionary<string, string> headers)
        {
            Task.Run(async () =>
            {
                if (_proxies == null)
                {
                    return;
                }

                var urlBase = $"{GetApiUrl()}/configs";

                await HttpClientHelper.Instance.PatchAsync(urlBase, headers);
            });
        }

        public async void ClashConfigReload(string filePath)
        {
            ClashConnectionClose("");
            try
            {
                var url = $"{GetApiUrl()}/configs?force=true";
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("path", filePath);
                await HttpClientHelper.Instance.PutAsync(url, headers);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public void GetClashConnections(Config config, Action<ClashConnections> updateFunc)
        {
            Task.Run(() => GetClashConnectionsAsync(config, updateFunc));
        }

        private async Task GetClashConnectionsAsync(Config config, Action<ClashConnections> updateFunc)
        {
            try
            {
                var url = $"{GetApiUrl()}/connections";
                var result = await HttpClientHelper.Instance.TryGetAsync(url);
                var clashConnections = JsonUtils.Deserialize<ClashConnections>(result);

                updateFunc?.Invoke(clashConnections);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        public async void ClashConnectionClose(string id)
        {
            try
            {
                var url = $"{GetApiUrl()}/connections/{id}";
                await HttpClientHelper.Instance.DeleteAsync(url);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
        }

        private string GetApiUrl()
        {
            return $"{Global.HttpProtocol}{Global.Loopback}:{AppHandler.Instance.StatePort2}";
        }
    }
}