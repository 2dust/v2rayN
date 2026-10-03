namespace ServiceLib.Manager;

public sealed class ClashApiManager
{
    private const string _tag = "ClashApiManager";
    private static readonly Lazy<ClashApiManager> instance = new(() => new());
    public static ClashApiManager Instance => instance.Value;

    private static string ApiUrl => $"{Global.HttpProtocol}{Global.Loopback}:{AppManager.Instance.StatePort2}";

    public async Task<ClashItem?> GetProxies()
    {
        for (var i = 0; i < 3; i++)
        {
            var url = $"{ApiUrl}/proxies";
            var resultTask = HttpClientHelper.Instance.TryGetAsync(url);

            var url2 = $"{ApiUrl}/providers/proxies";
            var result2Task = HttpClientHelper.Instance.TryGetAsync(url2);

            await Task.WhenAll(resultTask, result2Task);

            var result = await resultTask;
            var result2 = await result2Task;
            var clashProxies = JsonUtils.Deserialize<ClashProxies>(result);
            var clashProviders = JsonUtils.Deserialize<ClashProviders>(result2);

            if (clashProxies is null && clashProviders is null)
            {
                await Task.Delay(2000);
                continue;
            }

            var item = new ClashItem();
            if (clashProxies?.proxies is { Count: > 0 } proxies)
            {
                item = item with { Proxies = proxies };
            }
            if (clashProviders.providers is { Count: > 0 } providers)
            {
                foreach (var provider in providers)
                {
                    foreach (var proxy in provider.Value.proxies ?? [])
                    {
                        if (string.IsNullOrEmpty(proxy.name)
                            || !item.Proxies.TryAdd(proxy.name, proxy))
                        {
                            continue;
                        }
                        item.ProviderIndexMap.Add(proxy.name, provider.Key);
                    }
                }
            }

            return item;
        }

        return null;
    }

    public async Task<int> TestDelay(string name, ClashItem? clashItem)
    {
        if (clashItem?.ProviderIndexMap.TryGetValue(name, out var providerName) == true)
        {
            return await TestProviderProxyDelay(name, providerName);
        }
        return await TestProxyDelay(name);
    }

    private async Task<int> TestProxyDelay(string name)
    {
        var url = $"{ApiUrl}/proxies/{Utils.UrlEncode(name)}/delay?timeout=10000&url=" +
                  Utils.UrlEncode(AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl);
        return await SendTestRequest(url);
    }

    private async Task<int> TestProviderProxyDelay(string name, string providerName)
    {
        var url =
            $"{ApiUrl}/providers/proxies/{Utils.UrlEncode(providerName)}/{Utils.UrlEncode(name)}/healthcheck?timeout=10000&url=" +
            Utils.UrlEncode(AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl);
        return await SendTestRequest(url);
    }

    private async Task<int> SendTestRequest(string url)
    {
        var result = await HttpClientHelper.Instance.TryGetAsync(url);
        var jsonObject = JsonUtils.ParseJson(result) as JsonObject;
        return jsonObject?["delay"] is { } n && n.GetValueKind() == JsonValueKind.Number &&
               n.GetValue<JsonElement>().TryGetInt32(out var v)
            ? v
            : -1;
    }

    public async Task SetActiveProxy(string groupName, string nodeName)
    {
        try
        {
            var url = $"{ApiUrl}/proxies/{Utils.UrlEncode(groupName)}";
            var headers = new Dictionary<string, string>();
            headers.Add("name", nodeName);
            await HttpClientHelper.Instance.PutAsync(url, headers);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public async Task UpdateClashMode(string mode)
    {
        var headers = new Dictionary<string, string>
        {
            { "mode", mode },
        };
        await UpdateConfig(headers);
    }

    public async Task UpdateConfig(Dictionary<string, string> headers)
    {
        var urlBase = $"{ApiUrl}/configs";

        await HttpClientHelper.Instance.PatchAsync(urlBase, headers);
    }

    public async Task<List<string>> GetClashModes()
    {
        var jsonNode = await GetConfig();
        if ((jsonNode?["mode-list"] ?? jsonNode?["modes"]) is not JsonArray { Count: > 0 } modesArray)
        {
            return [];
        }
        var modes = new List<string>();
        foreach (var mode in modesArray)
        {
            if (mode is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String)
            {
                modes.Add(jsonValue.GetValue<string>());
            }
        }
        return modes;
    }

    public async Task<string?> GetClashMode()
    {
        var jsonNode = await GetConfig();
        if (jsonNode["mode"] is not JsonValue jsonValue || jsonValue.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }
        return jsonValue.GetValue<string>();
    }

    public async Task<JsonObject> GetConfig()
    {
        var url = $"{ApiUrl}/configs";
        var result = await HttpClientHelper.Instance.TryGetAsync(url);
        var jsonNode = JsonUtils.ParseJson(result);
        if (jsonNode is not JsonObject jsonObject)
        {
            return new JsonObject();
        }
        return jsonObject;
    }

    public async Task<ClashConnections?> GetConnections()
    {
        try
        {
            var url = $"{ApiUrl}/connections";
            var result = await HttpClientHelper.Instance.TryGetAsync(url);
            var clashConnections = JsonUtils.Deserialize<ClashConnections>(result);

            return clashConnections;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }

        return null;
    }

    public async Task CloseConnection(string id)
    {
        try
        {
            var url = $"{ApiUrl}/connections/{id}";
            await HttpClientHelper.Instance.DeleteAsync(url);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
