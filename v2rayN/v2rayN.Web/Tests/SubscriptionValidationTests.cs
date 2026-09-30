using ServiceLib.Enums;
using ServiceLib.Models.Entities;
using v2rayN.Web.Contracts;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class SubscriptionValidationTests
{
    [Test]
    public async Task AliasOnlySubscriptionIsValid()
    {
        var valid = V2rayRuntime.TryValidateSubscription(new SubscriptionInput("Manual group", string.Empty), out var code, out _);

        await valid.Should().BeTrue();
        await (code == "ok").Should().BeTrue();
    }

    [Test]
    public async Task NonEmptySubscriptionUrlStillRequiresAbsoluteHttpOrHttps()
    {
        var valid = V2rayRuntime.TryValidateSubscription(new SubscriptionInput("Remote group", "not-a-url"), out var code, out _);

        await valid.Should().BeFalse();
        await (code == "subscription_url_invalid").Should().BeTrue();
    }

    [Test]
    public async Task ValidAndEmptySubscriptionRequestHeadersAreAccepted()
    {
        foreach (var requestHeaders in new string?[] { "{\"X-Test\":\"value\"}", null, string.Empty, "  " })
        {
            var valid = V2rayRuntime.TryValidateSubscription(
                new SubscriptionInput("Remote group", "", RequestHeaders: requestHeaders), out var code, out _);

            await valid.Should().BeTrue();
            await code.Should().BeEqualTo("ok");
        }
    }

    [Test]
    public async Task InvalidSubscriptionRequestHeadersAreRejectedAsInvalidInput()
    {
        var valid = V2rayRuntime.TryValidateSubscription(
            new SubscriptionInput("Remote group", "", RequestHeaders: "[\"not-an-object\"]"), out var code, out var messageKey);

        await valid.Should().BeFalse();
        await code.Should().BeEqualTo("subscription_headers_invalid");
        await messageKey.Should().BeEqualTo(ApiMessageKeys.CommonInvalidInput);
    }

    [Test]
    public async Task InvalidRequestHeadersAreRejectedBeforeDatabaseAccess()
    {
        var runtime = new V2rayRuntime(null!, null!, null!, null!, null!);

        var result = await runtime.UpdateSubscriptionAsync("existing-subscription",
            new SubscriptionInput("Changed subscription", "", RequestHeaders: "[]"));

        await result.Success.Should().BeFalse();
        await result.Code.Should().BeEqualTo("subscription_headers_invalid");
    }

    [Test]
    public async Task SubscriptionUpdateCanClearCustomCoreTypeAndReloadAsNone()
    {
        var existing = new SubItem
        {
            Id = "existing-subscription",
            Remarks = "Existing subscription",
            Url = "https://example.test/subscription",
            CustomCoreType = ECoreType.mihomo,
        };
        var input = new SubscriptionInput("Existing subscription", "https://example.test/subscription",
            PreSocksPort: 10809, CustomCoreType: null);

        var updated = V2rayRuntime.ToSubItem(input, existing);
        var reloaded = V2rayRuntime.ToSubscriptionView(updated);

        await (existing.CustomCoreType == ECoreType.mihomo).Should().BeTrue();
        await (updated.CustomCoreType is null).Should().BeTrue();
        await (reloaded.CustomCoreType is null).Should().BeTrue();
        await updated.PreSocksPort.Should().BeEqualTo(10809);
    }
}
