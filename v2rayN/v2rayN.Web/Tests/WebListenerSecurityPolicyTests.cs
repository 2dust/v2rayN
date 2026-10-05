using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using v2rayN.Web.Security;

namespace v2rayN.Web.Tests;

public class WebListenerSecurityPolicyTests
{
    [Test]
    [Arguments("http://127.0.0.1:5080", false)]
    [Arguments("http://localhost:5080", false)]
    [Arguments("http://[::1]:5080", false)]
    [Arguments("http://0.0.0.0:5080", true)]
    [Arguments("http://[::]:5080", true)]
    [Arguments("http://192.168.1.20:5080", true)]
    [Arguments("http://*:5080", true)]
    [Arguments("http://+:5080", true)]
    [Arguments("http://nas:5080", true)]
    [Arguments("http://localhost.example:5080", true)]
    [Arguments("http://127.0.0.1:5080;https://[::1]:5443", false)]
    [Arguments("http://localhost:5080;http://0.0.0.0:5081", true)]
    [Arguments("http://192.168.1.20:5080;http://localhost:5081", true)]
    [Arguments("not-a-url", true)]
    public async Task AllEffectiveListenersMustBeLoopbackWithoutKey(string urls, bool requiresKey)
    {
        var configuration = Config(("urls", urls));
        await (WebListenerSecurityPolicy.GetStartupError(configuration, null) is not null).Should().BeEqualTo(requiresKey);
        await (WebListenerSecurityPolicy.GetStartupError(configuration, " \t") is not null).Should().BeEqualTo(requiresKey);
        await WebListenerSecurityPolicy.GetStartupError(configuration, "configured-key").Should().BeNull();
    }

    [Test]
    [Arguments("http_ports")]
    [Arguments("https_ports")]
    public async Task PortOnlyConfigurationIsWildcardButUrlsTakePrecedence(string portsKey)
    {
        await WebListenerSecurityPolicy.GetStartupError(Config((portsKey, "5080;5081")), null)
            .Should().BeEqualTo(WebListenerSecurityPolicy.MissingManagementKeyMessage);
        await WebListenerSecurityPolicy.GetStartupError(Config((portsKey, "5080"), ("urls", "http://localhost:5081")), null)
            .Should().BeNull();
    }

    [Test]
    public async Task KestrelEndpointsAndPreferHostingUrlsRespectBindingPrecedence()
    {
        var configuration = Config(("urls", "http://localhost:5080"), ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5081"));
        await WebListenerSecurityPolicy.GetStartupError(configuration, null)
            .Should().BeEqualTo(WebListenerSecurityPolicy.MissingManagementKeyMessage);
        configuration["preferHostingUrls"] = "true";
        await WebListenerSecurityPolicy.GetStartupError(configuration, null).Should().BeNull();
        configuration["urls"] = "http://0.0.0.0:5080";
        configuration["Kestrel:Endpoints:Http:Url"] = "http://[::1]:5081";
        await WebListenerSecurityPolicy.GetStartupError(configuration, null)
            .Should().BeEqualTo(WebListenerSecurityPolicy.MissingManagementKeyMessage);
        configuration["preferHostingUrls"] = "false";
        await WebListenerSecurityPolicy.GetStartupError(configuration, null).Should().BeNull();
        configuration["Kestrel:Endpoints:Other:Url"] = "http://192.168.1.20:5082";
        await WebListenerSecurityPolicy.GetStartupError(configuration, null)
            .Should().BeEqualTo(WebListenerSecurityPolicy.MissingManagementKeyMessage);
    }

    [Test]
    public async Task DefaultIsLoopbackAndDoesNotReplaceHttpsPorts()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        WebListenerSecurityPolicy.ApplyDefault(builder);
        await builder.Configuration["urls"].Should().BeEqualTo(WebListenerSecurityPolicy.DefaultUrl);
        await WebListenerSecurityPolicy.GetStartupError(builder.Configuration, null).Should().BeNull();
        builder.Configuration["urls"] = "";
        builder.Configuration["https_ports"] = "5443";
        WebListenerSecurityPolicy.ApplyDefault(builder);
        await WebListenerSecurityPolicy.GetStartupError(builder.Configuration, null)
            .Should().BeEqualTo(WebListenerSecurityPolicy.MissingManagementKeyMessage);
    }

    [Test]
    [Arguments("--urls", "http://localhost:5080", false)]
    [Arguments("--urls=http://0.0.0.0:5080", null, true)]
    [Arguments("--http_ports", "5080", true)]
    public async Task CommandLineOverridesPrefixedEnvironmentConfiguration(string argument, string? value, bool requiresKey)
    {
        // Same provider order as CreateBuilder: prefixed host configuration, then CLI.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = "http://192.168.1.20:5080" })
            .AddCommandLine(value is null ? [argument] : [argument, value]).Build();
        // URLs win over port-only configuration, so remove the URL for the ports-only case.
        if (argument == "--http_ports") configuration["urls"] = "";
        await (WebListenerSecurityPolicy.GetStartupError(configuration, null) is not null).Should().BeEqualTo(requiresKey);
    }

    private static IConfigurationRoot Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value)).Build();
}
