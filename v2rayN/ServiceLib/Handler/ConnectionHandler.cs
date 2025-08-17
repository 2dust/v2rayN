using System.Net;

namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";

    public static async Task<string> RunAvailabilityCheck()
    {
        var time = await GetRealPingTime();
        var ip = time > 0 ? await GetIPInfo() ?? Global.None : Global.None;

        return string.Format(ResUI.TestMeOutput, time, ip);
    }

    private static async Task<string?> GetIPInfo()
    {
        var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        var downloadHandle = new DownloadService();
        var result = await downloadHandle.TryDownloadString(url, true, "");
        if (result == null)
        {
            return null;
        }

        var ipInfo = JsonUtils.Deserialize<IPAPIInfo>(result);
        if (ipInfo == null)
        {
            return null;
        }

        var ip = ipInfo.ip ?? ipInfo.clientIp ?? ipInfo.ip_addr ?? ipInfo.query;
        var country = ipInfo.country_code ?? ipInfo.country ?? ipInfo.countryCode ?? ipInfo.location?.country_code;

        return $"({country ?? "unknown"}) {ip}";
    }

    private static async Task<int> GetRealPingTime()
    {
        var responseTime = -1;
        try
        {
            var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            var webProxy = new WebProxy($"socks5://{Global.Loopback}:{port}");
            var url = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;

            for (var i = 0; i < 2; i++)
            {
                responseTime = await HttpClientHelper.Instance.GetRealPingTime(url, webProxy, 10);
                if (responseTime > 0)
                {
                    break;
                }
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }
        return responseTime;
    }
}
