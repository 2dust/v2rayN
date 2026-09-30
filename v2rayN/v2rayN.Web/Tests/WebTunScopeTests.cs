using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ServiceLib.Enums;
using ServiceLib.Handler.Builder;
using ServiceLib.Models.Configs;
using ServiceLib.Models.CoreConfigs;
using ServiceLib.Models.Entities;
using v2rayN.Web.Api;
using v2rayN.Web.Contracts;
using v2rayN.Web.Security;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class WebTunScopeTests
{
    [Test]
    public async Task WebContractsDoNotExposeTunManagementButCoreDnsIncludesTunDns()
    {
        await HasProperty(typeof(WebSettingsView), "Tun").Should().BeFalse();
        await HasProperty(typeof(StatusView), "TunEnabled").Should().BeFalse();
        await HasProperty(typeof(StatusView), "TunInterfaceName").Should().BeFalse();
        await HasProperty(typeof(StatusView), "TunInterfaceActive").Should().BeFalse();
        await HasProperty(typeof(DnsProfileView), "TunDNS").Should().BeTrue();
        await HasProperty(typeof(DnsProfileInput), "TunDNS").Should().BeTrue();
        await HasProperty(typeof(CoreConfigTemplateView), "TunConfig").Should().BeTrue();
        await HasProperty(typeof(CoreConfigTemplateInput), "TunConfig").Should().BeTrue();
    }

    [Test]
    public async Task TunSettingsRouteIsNotRegistered()
    {
        using var sessions = new WebSessionService();
        var session = sessions.CreateSession();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(sessions);
        builder.Services.AddSingleton<EventHub>();
        builder.Services.AddSingleton<LogBuffer>();
        builder.Services.AddSingleton<RuntimeOperationCoordinator>();
        builder.Services.AddSingleton<V2rayRuntime>();

        await using var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<WebSessionAuthenticationMiddleware>();
        app.MapWebApi();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/settings/tun");
            request.Headers.Authorization = new("Bearer", session.Token);
            using var response = await client.SendAsync(request);

            await (response.StatusCode == HttpStatusCode.NotFound).Should().BeTrue();
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Test]
    public async Task GeneratedMainOrPreContextWithTunIsRejectedWithoutChangingSavedConfig()
    {
        var config = new Config();
        config.TunModeItem = new ServiceLib.Models.Configs.TunModeItem();
        config.TunModeItem.EnableTun = true;
        var main = CreateContextResult(isTunEnabled: false);
        var noTun = V2rayRuntime.GetTunLaunchRejection(new CoreConfigContextBuilderAllResult(main, null));
        await (noTun is null).Should().BeTrue();

        var mainTun = V2rayRuntime.GetTunLaunchRejection(
            new CoreConfigContextBuilderAllResult(CreateContextResult(isTunEnabled: true), null));
        await (mainTun?.Code == "tun_not_supported").Should().BeTrue();
        await (mainTun?.MessageKey == ApiMessageKeys.CoreTunNotSupported).Should().BeTrue();

        var preTun = V2rayRuntime.GetTunLaunchRejection(
            new CoreConfigContextBuilderAllResult(main, CreateContextResult(isTunEnabled: true)));
        await (preTun?.Code == "tun_not_supported").Should().BeTrue();

        var invalidTun = V2rayRuntime.GetTunLaunchRejection(
            new CoreConfigContextBuilderAllResult(CreateContextResult(isTunEnabled: true, valid: false), null));
        await (invalidTun?.Code == "tun_not_supported").Should().BeTrue();
        await config.TunModeItem.EnableTun.Should().BeTrue();
    }

    private static bool HasProperty(Type type, string name) =>
        type.GetProperties().Any(property => property.Name == name);

    private static CoreConfigContextBuilderResult CreateContextResult(bool isTunEnabled, bool valid = true) =>
        new(
            new CoreConfigContext
            {
                Node = new ProfileItem(),
                RunCoreType = ECoreType.Xray,
                IsTunEnabled = isTunEnabled,
            },
            valid ? NodeValidatorResult.Empty() : new NodeValidatorResult(["invalid"], []));
}
