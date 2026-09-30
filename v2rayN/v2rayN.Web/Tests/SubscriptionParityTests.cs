using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ServiceLib;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Models.Entities;
using ServiceLib.Services;
using v2rayN.Web.Contracts;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

[NotInParallel]
public class SubscriptionParityTests
{
    [Test]
    public async Task EveryEditableSubItemFieldRoundTripsAndIntervalZeroStaysZero()
    {
        var original = new SubItem
        {
            Id = "subscription-parity",
            Remarks = "Manual group",
            Url = "https://main.example/sub",
            MoreUrl = "https://a.example/sub,https://b.example/sub",
            Enabled = false,
            UserAgent = "parity-agent",
            RequestHeaders = "{\"X-Test\":\"value\"}",
            Sort = 17,
            Filter = "^edge",
            AutoUpdateInterval = 0,
            ConvertTarget = "mixed",
            PrevProfile = "typed previous alias",
            NextProfile = "typed landing alias",
            PreSocksPort = 10809,
            Memo = "memo",
            CustomCoreType = ECoreType.sing_box,
        };

        var loaded = V2rayRuntime.ToSubscriptionView(original);
        var input = new SubscriptionInput(
            loaded.Remarks,
            loaded.Url,
            loaded.MoreUrl,
            loaded.Enabled,
            loaded.UserAgent,
            loaded.RequestHeaders,
            loaded.Filter,
            loaded.AutoUpdateInterval,
            loaded.ConvertTarget,
            loaded.Memo,
            loaded.Sort,
            loaded.PrevProfile,
            loaded.NextProfile,
            loaded.PreSocksPort,
            ECoreType.sing_box);
        var saved = V2rayRuntime.ToSubItem(input, original);
        var reloaded = V2rayRuntime.ToSubscriptionView(saved);

        await reloaded.Remarks.Should().BeEqualTo(original.Remarks);
        await reloaded.Url.Should().BeEqualTo(original.Url);
        await reloaded.MoreUrl.Should().BeEqualTo(original.MoreUrl);
        await reloaded.Enabled.Should().BeEqualTo(original.Enabled);
        await reloaded.UserAgent.Should().BeEqualTo(original.UserAgent);
        await reloaded.RequestHeaders.Should().BeEqualTo(original.RequestHeaders);
        await reloaded.Filter.Should().BeEqualTo(original.Filter);
        await reloaded.AutoUpdateInterval.Should().BeEqualTo(0);
        await reloaded.ConvertTarget.Should().BeEqualTo(original.ConvertTarget);
        await reloaded.Memo.Should().BeEqualTo(original.Memo);
        await reloaded.Sort.Should().BeEqualTo(original.Sort);
        await reloaded.PrevProfile.Should().BeEqualTo("typed previous alias");
        await reloaded.NextProfile.Should().BeEqualTo("typed landing alias");
        await reloaded.PreSocksPort.Should().BeEqualTo(original.PreSocksPort);
        await reloaded.CustomCoreType.Should().BeEqualTo("sing_box");
    }

    [Test]
    public async Task NullAndEmptySubscriptionFieldsAreNotCollapsedDuringMapping()
    {
        var original = new SubItem
        {
            Id = "nullable-subscription",
            Remarks = "Nullable values",
            Url = "",
            MoreUrl = "",
            RequestHeaders = null,
            Filter = null,
            ConvertTarget = null,
            Memo = null,
            AutoUpdateInterval = 0,
        };
        var loaded = V2rayRuntime.ToSubscriptionView(original);
        var untouched = V2rayRuntime.ToSubItem(new SubscriptionInput(loaded.Remarks, loaded.Url,
            MoreUrl: loaded.MoreUrl, RequestHeaders: loaded.RequestHeaders, Filter: loaded.Filter,
            ConvertTarget: loaded.ConvertTarget, Memo: loaded.Memo, AutoUpdateInterval: loaded.AutoUpdateInterval), original);
        var cleared = V2rayRuntime.ToSubItem(new SubscriptionInput(loaded.Remarks, loaded.Url,
            MoreUrl: string.Empty, RequestHeaders: string.Empty, Filter: string.Empty,
            ConvertTarget: string.Empty, Memo: string.Empty, AutoUpdateInterval: 0), original);

        await untouched.RequestHeaders.Should().BeNull();
        await untouched.Filter.Should().BeNull();
        await untouched.ConvertTarget.Should().BeNull();
        await untouched.Memo.Should().BeNull();
        await cleared.RequestHeaders.Should().BeEqualTo(string.Empty);
        await cleared.Filter.Should().BeEqualTo(string.Empty);
        await cleared.ConvertTarget.Should().BeEqualTo(string.Empty);
        await cleared.Memo.Should().BeEqualTo(string.Empty);
        await cleared.AutoUpdateInterval.Should().BeEqualTo(0);
    }

    [Test]
    public async Task MoreUrlDownloadsMainAndEveryAdditionalUrlAndMergesTheirContent()
    {
        await ServiceLib.Manager.CertPemManager.Instance.Init(
            new ServiceLib.Models.Configs.Config { GuiItem = new ServiceLib.Models.Configs.GUIItem() });
        var visited = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.MapGet("/main", () => Respond("/main", "main-node.example"));
        app.MapGet("/a", () => Respond("/a", "additional-a.example"));
        app.MapGet("/b", () => Respond("/b", "additional-b.example"));
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var subscription = new SubItem
            {
                Id = "multiple-urls",
                Remarks = "Three URLs",
                Url = $"{address.TrimEnd('/')}/main",
                MoreUrl = $"{address.TrimEnd('/')}/a,{address.TrimEnd('/')}/b",
                UserAgent = string.Empty,
                ConvertTarget = string.Empty,
            };

            var download = typeof(SubscriptionHandler).GetMethod("DownloadAllSubscriptions", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SubscriptionHandler).FullName, "DownloadAllSubscriptions");
            var task = (Task<string>?)download.Invoke(null, [new ServiceLib.Models.Configs.Config(), subscription, false, new DownloadService()])
                ?? throw new InvalidOperationException("The ServiceLib subscription download task was not created.");
            var merged = await task;

            await visited.ToArray().SequenceEqual(new[] { "/main", "/a", "/b" }).Should().BeTrue();
            await merged.Contains("main-node.example").Should().BeTrue();
            await merged.Contains("additional-a.example").Should().BeTrue();
            await merged.Contains("additional-b.example").Should().BeTrue();
        }
        finally
        {
            await app.StopAsync();
        }

        IResult Respond(string path, string body)
        {
            visited.Enqueue(path);
            return Results.Text(body);
        }
    }

    [Test]
    public async Task ServiceLibKeepsConversionAndMoreUrlMutuallyExclusive()
    {
        await ServiceLib.Manager.CertPemManager.Instance.Init(
            new ServiceLib.Models.Configs.Config { GuiItem = new ServiceLib.Models.Configs.GUIItem() });
        var visited = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.MapGet("/convert", () =>
        {
            visited.Enqueue("/convert");
            return Results.Text("converted-node");
        });
        app.MapGet("/a", () =>
        {
            visited.Enqueue("/a");
            return Results.Text("must-not-be-downloaded");
        });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var subscription = new SubItem
            {
                Id = "converted-subscription",
                Remarks = "Converted subscription",
                Url = "https://main.example/sub",
                MoreUrl = $"{address.TrimEnd('/')}/a",
                ConvertTarget = "mixed",
            };
            var config = new ServiceLib.Models.Configs.Config
            {
                ConstItem = new ServiceLib.Models.Configs.ConstItem(),
            };
            config.ConstItem.SubConvertUrl = $"{address.TrimEnd('/')}/convert?url={{0}}";
            var download = typeof(SubscriptionHandler).GetMethod("DownloadAllSubscriptions", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SubscriptionHandler).FullName, "DownloadAllSubscriptions");
            var task = (Task<string>?)download.Invoke(null, [config, subscription, false, new DownloadService()])
                ?? throw new InvalidOperationException("The ServiceLib subscription download task was not created.");

            var merged = await task;
            await visited.ToArray().SequenceEqual(new[] { "/convert" }).Should().BeTrue();
            await merged.Should().BeEqualTo("converted-node");
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
