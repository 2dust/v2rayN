using Microsoft.AspNetCore.Http;
using v2rayN.WebAPI.Security;

namespace v2rayN.WebAPI.Tests;

public class WebSecurityHeadersTests
{
    [Test]
    public async Task BrowserResponsesReceiveSecurityHeadersAndSelfHostedCsp()
    {
        var context = new DefaultHttpContext();
        var downstreamCalled = false;
        var middleware = new WebSecurityHeadersMiddleware(_ =>
        {
            downstreamCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        await downstreamCalled.Should().BeTrue();
        await context.Response.Headers["X-Content-Type-Options"].ToString().Should().BeEqualTo("nosniff");
        await context.Response.Headers["Referrer-Policy"].ToString().Should().BeEqualTo("no-referrer");
        await context.Response.Headers["X-Frame-Options"].ToString().Should().BeEqualTo("DENY");
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        await csp.Should().Contain("frame-ancestors 'none'");
        await csp.Should().Contain("script-src 'self'");
        await csp.Should().Contain("connect-src 'self'");
        await csp.Should().Contain("style-src 'self' 'unsafe-inline'");
        await csp.Should().NotContain("'unsafe-eval'");
        await csp.Should().NotContain("https:");
        await csp.Should().NotContain("ws:");
        await csp.Should().NotContain("data:");
    }
}
