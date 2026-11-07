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

    public HttpMessageHandler DirectHandler => new SharedHandlerAdapter(_directHandlerLazy.Value);

    public HttpMessageHandler ProxyHandler
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
            return new SharedHandlerAdapter(field);
        }
    } = null!;

    public HttpMessageHandler GetHandler(bool useProxy)
    {
        return useProxy ? ProxyHandler : DirectHandler;
    }

    public HttpClient GetHttpClient(bool useProxy)
    {
        var handler = GetHandler(useProxy);
        var client = new HttpClient(handler);
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

    public static SocketsHttpHandler CreateSocketsHttpHandler(bool useProxy, CookieContainer cookieContainer)
    {
        var handler = CreateSocketsHttpHandler(useProxy);
        handler.UseCookies = true;
        handler.CookieContainer = cookieContainer;
        return handler;
    }

    public static SocketsHttpHandler CreateProxySocketsHttpHandler(IWebProxy proxy)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            Proxy = proxy,
            ConnectTimeout = Global.ProxyDownloadConnect,
        };
        var certificateChainPolicy = CertPemManager.Instance.BuildCertificateChainPolicy();
        if (certificateChainPolicy != null)
        {
            handler.SslOptions.CertificateChainPolicy = certificateChainPolicy;
            handler.SslOptions.RemoteCertificateValidationCallback = null;
        }
        return handler;
    }

    public static SocketsHttpHandler CreateProxySocketsHttpHandler(int port)
    {
        return CreateProxySocketsHttpHandler(new WebProxy($"socks5://{Global.Loopback}:{port}"));
    }

    public static SocketsHttpHandler CreateProxySocketsHttpHandler(IWebProxy proxy, CookieContainer cookieContainer)
    {
        var handler = CreateProxySocketsHttpHandler(proxy);
        handler.UseCookies = true;
        handler.CookieContainer = cookieContainer;
        return handler;
    }

    public static SocketsHttpHandler CreateProxySocketsHttpHandler(int port, CookieContainer cookieContainer)
    {
        var handler = CreateProxySocketsHttpHandler(port);
        handler.UseCookies = true;
        handler.CookieContainer = cookieContainer;
        return handler;
    }

    public HttpMessageHandler CreateFallbackHandler(bool preferProxy)
    {
        var primaryHandler = preferProxy ? ProxyHandler : DirectHandler;
        var fallbackHandler = preferProxy ? DirectHandler : ProxyHandler;
        return new DualHandlerFallbackHandler(primaryHandler, fallbackHandler);
    }

    public HttpMessageHandler CreateFallbackHandler(bool preferProxy, CookieContainer cookieContainer)
    {
        var primaryHandler = preferProxy ? CreateSocketsHttpHandler(true, cookieContainer) : CreateSocketsHttpHandler(false, cookieContainer);
        var fallbackHandler = preferProxy ? CreateSocketsHttpHandler(false, cookieContainer) : CreateSocketsHttpHandler(true, cookieContainer);
        return new DualHandlerFallbackHandler(primaryHandler, fallbackHandler);
    }

    private class DualHandlerFallbackHandler(
        HttpMessageHandler primaryHandler,
        HttpMessageHandler fallbackHandler)
        : DelegatingHandler(primaryHandler)
    {
        private readonly HttpMessageInvoker _fallbackInvoker = new(fallbackHandler);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var contentBytes = await BufferContentAsync(request, cancellationToken);

            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                if (IsRetriableStatusCode(response.StatusCode))
                {
                    response.Dispose();
                    ResetRequestContent(request, contentBytes);
                    return await _fallbackInvoker.SendAsync(request, cancellationToken);
                }

                return response;
            }
            catch (Exception ex) when (IsNetworkOrTimeoutException(ex) && !cancellationToken.IsCancellationRequested)
            {
                ResetRequestContent(request, contentBytes);
                return await _fallbackInvoker.SendAsync(request, cancellationToken);
            }
        }

        private static async Task<byte[]?> BufferContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content == null)
            {
                return null;
            }
            return await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        private static void ResetRequestContent(HttpRequestMessage request, byte[]? contentBytes)
        {
            if (contentBytes == null)
            {
                return;
            }

            var newContent = new ByteArrayContent(contentBytes);

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            request.Content = newContent;
        }

        private static bool IsNetworkOrTimeoutException(Exception ex)
        {
            return ex is HttpRequestException or TaskCanceledException;
        }

        private static bool IsRetriableStatusCode(HttpStatusCode statusCode) =>
            statusCode is HttpStatusCode.BadGateway          // 502
                or HttpStatusCode.ServiceUnavailable   // 503
                or HttpStatusCode.GatewayTimeout;      // 504

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fallbackInvoker.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class SharedHandlerAdapter(HttpMessageHandler sharedHandler) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _invoker = new(
            sharedHandler,
            disposeHandler: false);

        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _invoker.Send(request, cancellationToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _invoker.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _invoker.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
