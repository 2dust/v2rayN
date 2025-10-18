using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace ServiceLib.Services;

/// <summary>
///Download
/// </summary>
public class DownloadService
{
    public event EventHandler<RetResult>? UpdateCompleted;

    public event ErrorEventHandler? Error;

    private static readonly string _tag = "DownloadService";

    public async Task<int> DownloadDataAsync(string url, WebProxy webProxy, int downloadTimeout, Func<bool, string, Task> updateFunc)
    {
        try
        {
            SetSecurityProtocol(AppManager.Instance.Config.GuiItem.EnableSecurityProtocolTls13);

            var progress = new Progress<string>();
            progress.ProgressChanged += (sender, value) => updateFunc?.Invoke(false, $"{value}");

            await DownloaderHelper.Instance.DownloadDataAsync4Speed(webProxy,
                  url,
                  progress,
                  downloadTimeout);
        }
        catch (Exception ex)
        {
            await updateFunc?.Invoke(false, ex.Message);
            if (ex.InnerException != null)
            {
                await updateFunc?.Invoke(false, ex.InnerException.Message);
            }
        }
        return 0;
    }

    public async Task DownloadFileAsync(string url, string fileName, bool blProxy, int downloadTimeout)
    {
        try
        {
            SetSecurityProtocol(AppManager.Instance.Config.GuiItem.EnableSecurityProtocolTls13);
            UpdateCompleted?.Invoke(this, new RetResult(false, $"{ResUI.Downloading}   {url}"));

            var progress = new Progress<double>();
            progress.ProgressChanged += (sender, value) => UpdateCompleted?.Invoke(this, new RetResult(value > 100, $"...{value}%"));

            var webProxy = await GetWebProxy(blProxy);
            await DownloaderHelper.Instance.DownloadFileAsync(webProxy,
                url,
                fileName,
                progress,
                downloadTimeout);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);

            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
    }

    public async Task<string?> UrlRedirectAsync(string url, bool blProxy)
    {
        SetSecurityProtocol(AppManager.Instance.Config.GuiItem.EnableSecurityProtocolTls13);
        var webRequestHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            Proxy = await GetWebProxy(blProxy)
        };
        HttpClient client = new(webRequestHandler);

        var response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is not null)
        {
            return response.Headers.Location.ToString();
        }
        else
        {
            Error?.Invoke(this, new ErrorEventArgs(new Exception("StatusCode error: " + response.StatusCode)));
            Logging.SaveLog("StatusCode error: " + url);
            return null;
        }
    }

    public async Task<string?> TryDownloadString(string url, bool blProxy, string userAgent)
    {
        try
        {
            var result1 = await DownloadStringAsync(url, blProxy, userAgent, 15);
            if (result1.IsNotEmpty())
            {
                return result1;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        try
        {
            var result2 = await DownloadStringViaDownloader(url, blProxy, userAgent, 15);
            if (result2.IsNotEmpty())
            {
                return result2;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        return null;
    }

    // Best-effort: get specific header (Subscription-Userinfo) and content in one request.
    // Returns: (userinfoHeaderValue, content)
    public async Task<(string? userInfoHeader, string? content)> TryGetWithHeaders(string url, bool blProxy, string userAgent, int timeoutSeconds = 15)
    {
        try
        {
            SetSecurityProtocol(AppManager.Instance.Config.GuiItem.EnableSecurityProtocolTls13);
            var webProxy = await GetWebProxy(blProxy);
            var handler = new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null,
                AllowAutoRedirect = true
            };
            using var client = new HttpClient(handler);

            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);

            Uri uri = new(url);
            if (uri.UserInfo.IsNotEmpty())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            // Try read target header regardless of status code
            string? userHeader = null;
            if (resp.Headers != null)
            {
                if (resp.Headers.TryGetValues("Subscription-Userinfo", out var vals) ||
                    resp.Headers.TryGetValues("subscription-userinfo", out vals))
                {
                    userHeader = vals?.FirstOrDefault();
                }
            }

            // Read content only on success; otherwise empty to trigger fallback
            string? content = null;
            if (resp.IsSuccessStatusCode)
            {
                content = await resp.Content.ReadAsStringAsync(cts.Token);
            }
            return (userHeader, content);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
        return (null, null);
    }

    /// <summary>
    /// DownloadString
    /// </summary>
    /// <param name="url"></param>
    private async Task<string?> DownloadStringAsync(string url, bool blProxy, string userAgent, int timeout)
    {
        try
        {
            SetSecurityProtocol(AppManager.Instance.Config.GuiItem.EnableSecurityProtocolTls13);
            var webProxy = await GetWebProxy(blProxy);
            var client = new HttpClient(new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null
            });

            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);

            Uri uri = new(url);
            //Authorization Header
            if (uri.UserInfo.IsNotEmpty())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
            }

            using var cts = new CancellationTokenSource();
            var result = await HttpClientHelper.Instance.GetAsync(client, url, cts.Token).WaitAsync(TimeSpan.FromSeconds(timeout), cts.Token);
            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
        return null;
    }

    /// <summary>
    /// DownloadString
    /// </summary>
    /// <param name="url"></param>
    private async Task<string?> DownloadStringViaDownloader(string url, bool blProxy, string userAgent, int timeout)
    {
        try
        {
            SetSecurityProtocol(AppManager.Instance.Config.GuiItem.EnableSecurityProtocolTls13);

            var webProxy = await GetWebProxy(blProxy);

            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            var result = await DownloaderHelper.Instance.DownloadStringAsync(webProxy, url, userAgent, timeout);
            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
        return null;
    }

    private async Task<WebProxy?> GetWebProxy(bool blProxy)
    {
        if (!blProxy)
        {
            return null;
        }
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        if (await SocketCheck(Global.Loopback, port) == false)
        {
            return null;
        }

        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    private async Task<bool> SocketCheck(string ip, int port)
    {
        try
        {
            IPEndPoint point = new(IPAddress.Parse(ip), port);
            using Socket? sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await sock.ConnectAsync(point);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void SetSecurityProtocol(bool enableSecurityProtocolTls13)
    {
        if (enableSecurityProtocolTls13)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }
        else
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        ServicePointManager.DefaultConnectionLimit = 256;
    }
}
