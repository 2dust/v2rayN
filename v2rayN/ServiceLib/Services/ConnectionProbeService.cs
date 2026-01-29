using System.Net.NetworkInformation;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace ServiceLib.Services;

public class ConnectionProbeService
{
    private static readonly string _tag = "ConnectionProbeService";
    private readonly Config _config;

    public ConnectionProbeService(Config config)
    {
        _config = config;
    }

    public async Task<ReportResult> ProbeAndReportAsync(ProfileItem profile, Func<bool, string, Task> updateFunc = null)
    {
        var result = new ReportResult();
        
        if (updateFunc != null) await updateFunc(false, "Starting Connection Probe & Report...");

        // 1. Gather System Info
        result.SystemInfo = GetSystemInfo();

        // 2. Test Connection (Ping)
        // Using existing ConnectionHandler logic but simplified for single target
        try
        {
            var webProxy = new WebProxy($"socks5://{Global.Loopback}:{profile.Port}");
            var url = _config.SpeedTestItem.SpeedPingTestUrl;
            var responseTime = await ConnectionHandler.GetRealPingTime(url, webProxy, 10);
            
            result.ConnectionQuality = new ConnectionQuality
            {
                Latency = responseTime,
                Success = responseTime > 0
            };
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            result.ConnectionQuality = new ConnectionQuality { Success = false, Message = ex.Message };
        }

        // 3. Populate Protocol Info
        result.ProtocolInfo = new ProtocolInfo
        {
            Protocol = profile.ConfigType.ToString(),
            Remarks = profile.Remarks,
            Address = profile.Address
        };

        // 4. Send Report
        if (!string.IsNullOrEmpty(_config.ReportItem.ReportUrl))
        {
            if (updateFunc != null) await updateFunc(false, "Sending Connection Report...");
            await SendReportAsync(result);
            if (updateFunc != null) await updateFunc(false, "Connection Report Sent Successfully.");
        }

        return result;
    }

    private SystemInfo GetSystemInfo()
    {
        var info = new SystemInfo();
        try
        {
            // Simple heuristic to guess active interface
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            if (interfaces.Count > 0)
            {
                // Prioritize Ethernet/Wifi
                var bestMatch = interfaces.FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) 
                                ?? interfaces.First();

                info.InterfaceName = bestMatch.Name;
                info.InterfaceDescription = bestMatch.Description;
                info.InterfaceType = bestMatch.NetworkInterfaceType.ToString();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            info.Message = "Failed to gather system info";
        }
        return info;
    }

    private async Task SendReportAsync(ReportResult report)
    {
        try
        {
            using var client = new HttpClient();
            var json = JsonSerializer.Serialize(report);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_config.ReportItem.ReportUrl, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            throw; // Re-throw to show error in UI
        }
    }
}

public class ReportResult
{
    public SystemInfo SystemInfo { get; set; }
    public ConnectionQuality ConnectionQuality { get; set; }
    public ProtocolInfo ProtocolInfo { get; set; }
}

public class SystemInfo
{
    public string InterfaceName { get; set; }
    public string InterfaceDescription { get; set; } // Often contains SIM/ISP info (e.g., "Intel Wi-Fi", "Quectel Mobile Broadband")
    public string InterfaceType { get; set; }
    public string Message { get; set; }
}

public class ConnectionQuality
{
    public int Latency { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
}

public class ProtocolInfo
{
    public string Protocol { get; set; }
    public string Remarks { get; set; }
    public string Address { get; set; }
}
