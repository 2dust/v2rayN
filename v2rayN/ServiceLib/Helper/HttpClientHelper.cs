namespace ServiceLib.Helper;

/// <summary>
/// </summary>
public class HttpClientHelper
{
    private static readonly Lazy<HttpClientHelper> _instance = new(() => new());

    // private readonly SocketsHttpHandler _directHandler = CreateSocketsHttpHandler(false);
    private readonly Lazy<SocketsHttpHandler> _directHandlerLazy = new(() => CreateSocketsHttpHandler(false));
    private Uri _proxyUri = null!;

    public static HttpClientHelper Instance => _instance.Value;

    public SocketsHttpHandler DirectHandler => _directHandlerLazy.Value;

    public SocketsHttpHandler ProxyHandler
    {
        get
        {
            var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            var proxyUri = new Uri($"socks5://{Global.Loopback}:{port}");
            if (_proxyUri != proxyUri)
            {
                //field?.Dispose();
                _proxyUri = proxyUri;
                field = CreateProxySocketsHttpHandler(port);
            }
            return field;
        }
    } = null!;

    public SocketsHttpHandler GetHandler(bool useProxy)
    {
        return useProxy ? ProxyHandler : DirectHandler;
    }

    public HttpClient GetHttpClient(bool useProxy)
    {
        var handler = GetHandler(useProxy);
        var client = new HttpClient(handler, false);
        return client;
    }

    public static SocketsHttpHandler CreateSocketsHttpHandler(bool useProxy)
    {
        SocketsHttpHandler handler;
        if (!useProxy)
        {
            handler = new()
            {
                UseProxy = false,
                UseCookies = false,
                ConnectTimeout = Global.DirectDownloadConnect,
            };
        }
        else
        {
            var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            handler = CreateProxySocketsHttpHandler(port);
        }
        var certificateChainPolicy = CertPemManager.Instance.BuildCertificateChainPolicy();
        if (certificateChainPolicy != null)
        {
            handler.SslOptions.CertificateChainPolicy = certificateChainPolicy;
            handler.SslOptions.RemoteCertificateValidationCallback = null;
        }
        return handler;
    }

    public static SocketsHttpHandler CreateProxySocketsHttpHandler(IWebProxy proxy)
    {
        return new SocketsHttpHandler
        {
            UseCookies = false,
            Proxy = proxy,
            ConnectTimeout = Global.ProxyDownloadConnect,
        };
    }

    public static SocketsHttpHandler CreateProxySocketsHttpHandler(int port)
    {
        return new SocketsHttpHandler
        {
            UseCookies = false,
            Proxy = new WebProxy($"socks5://{Global.Loopback}:{port}"),
            ConnectTimeout = Global.ProxyDownloadConnect,
        };
    }
}
