namespace ServiceLib.Handler;

public class ConnectionHandler
{
    private static readonly Lazy<ConnectionHandler> _instance = new(() => new());
    public static ConnectionHandler Instance => _instance.Value;

    public async Task<string> RunAvailabilityCheck()
    {
        var downloadHandle = new DownloadService();
        var time = await downloadHandle.RunAvailabilityCheck(null);
        var ip = time > 0 ? await GetIPInfo(downloadHandle) ?? Global.None : Global.None;

        return string.Format(ResUI.TestMeOutput, time, ip);
    }

    private async Task<string?> GetIPInfo(DownloadService downloadHandle)
    {
        var url = AppHandler.Instance.Config.SpeedTestItem.IPAPIUrl;
        if (url.IsNullOrEmpty())
        {
            return null;
        }

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
}
