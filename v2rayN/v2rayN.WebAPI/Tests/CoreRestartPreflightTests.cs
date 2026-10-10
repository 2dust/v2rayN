using System.Net;
using System.Net.Sockets;
using ServiceLib.Enums;
using ServiceLib.Handler.Builder;
using ServiceLib.Models.CoreConfigs;
using ServiceLib.Models.Entities;
using v2rayN.WebAPI.Contracts;
using v2rayN.WebAPI.Services;

namespace v2rayN.WebAPI.Tests;

public class CoreRestartPreflightTests
{
    [Test]
    public async Task RestartWithTunContextKeepsRunningCore()
    {
        var preflight = CoreLaunchPreflight.Validate(
            new ProfileItem(),
            CreateBuiltContext(isTunEnabled: true),
            _ => null);

        await AssertRestartRejectedWithoutStoppingCore(preflight, "tun_not_supported");
    }

    [Test]
    public async Task RestartWithInvalidContextKeepsRunningCore()
    {
        var binaryCheckCalled = false;
        var preflight = CoreLaunchPreflight.Validate(
            new ProfileItem(),
            CreateBuiltContext(valid: false),
            _ =>
            {
                binaryCheckCalled = true;
                return null;
            });

        await AssertRestartRejectedWithoutStoppingCore(preflight, "profile_validation_failed");
        await binaryCheckCalled.Should().BeFalse();
    }

    [Test]
    public async Task RestartWithMissingCoreBinaryKeepsRunningCore()
    {
        var preflight = CoreLaunchPreflight.Validate(
            new ProfileItem(),
            CreateBuiltContext(),
            coreType => OperationView.Fail("core_binary_missing", ApiMessageKeys.CoreBinaryMissing,
                new { coreType = coreType.ToString() }));

        await AssertRestartRejectedWithoutStoppingCore(preflight, "core_binary_missing");
    }

    [Test]
    public async Task ValidRestartStopsThenLaunchesThePreflightedContext()
    {
        var profile = new ProfileItem { IndexId = "profile-1" };
        var builtContext = CreateBuiltContext();
        var preflight = CoreLaunchPreflight.Validate(profile, builtContext, _ => null);
        var events = new List<string>();
        var coreRunning = true;
        CoreConfigContextBuilderAllResult? launchedContext = null;

        var result = await CoreRestartFlow.ExecuteAsync(
            () => Task.FromResult(preflight),
            () =>
            {
                events.Add("stop");
                coreRunning = false;
                return Task.CompletedTask;
            },
            () => { },
            plan =>
            {
                events.Add("launch");
                launchedContext = plan.BuiltContext;
                coreRunning = true;
                return Task.FromResult(OperationView.Ok(ApiMessageKeys.CoreStarted,
                    new { profileId = plan.Profile.IndexId }));
            });

        await events.Count.Should().BeEqualTo(2);
        await events[0].Should().BeEqualTo("stop");
        await events[1].Should().BeEqualTo("launch");
        await ReferenceEquals(launchedContext, builtContext).Should().BeTrue();
        await coreRunning.Should().BeTrue();
        await result.Success.Should().BeTrue();
        await result.MessageKey.Should().BeEqualTo(ApiMessageKeys.CoreRestarted);
    }

    [Test]
    public async Task UnrelatedListenerIsReportedUnavailableByTheNormalStartPreflightProbe()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var target = new RuntimeListenerSnapshot("local", ["http", "socks"], "127.0.0.1", port);

            // With no Core owned by Web, the probe must reject the port before launch.
            var unavailableForUnrelatedOwner = await V2rayRuntime.IsProjectedListenerUnavailableAsync(
                target,
                runtimeOwned: [],
                CancellationToken.None);
            await unavailableForUnrelatedOwner.Should().BeTrue();

            // The same listener is fine while Web still owns the tracked Core process.
            var unavailableForOwnedListener = await V2rayRuntime.IsProjectedListenerUnavailableAsync(
                target,
                runtimeOwned: [target],
                CancellationToken.None);
            await unavailableForOwnedListener.Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task AssertRestartRejectedWithoutStoppingCore(
        CoreLaunchPreflightResult preflight,
        string expectedCode)
    {
        var coreRunning = true;
        var stopCalled = false;
        var launchCalled = false;

        var result = await CoreRestartFlow.ExecuteAsync(
            () => Task.FromResult(preflight),
            () =>
            {
                stopCalled = true;
                coreRunning = false;
                return Task.CompletedTask;
            },
            () => { },
            _ =>
            {
                launchCalled = true;
                return Task.FromResult(OperationView.Ok(ApiMessageKeys.CoreStarted));
            });

        await result.Code.Should().BeEqualTo(expectedCode);
        await stopCalled.Should().BeFalse();
        await launchCalled.Should().BeFalse();
        await coreRunning.Should().BeTrue();
    }

    private static CoreConfigContextBuilderAllResult CreateBuiltContext(
        bool isTunEnabled = false,
        bool valid = true)
    {
        var validator = valid
            ? NodeValidatorResult.Empty()
            : new NodeValidatorResult(["invalid"], []);
        return new CoreConfigContextBuilderAllResult(
            new CoreConfigContextBuilderResult(
                new CoreConfigContext
                {
                    Node = new ProfileItem(),
                    RunCoreType = ECoreType.Xray,
                    IsTunEnabled = isTunEnabled,
                },
                validator),
            null);
    }
}
