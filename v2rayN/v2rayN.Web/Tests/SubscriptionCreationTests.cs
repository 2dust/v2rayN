using System.Reflection;
using ServiceLib;
using ServiceLib.Helper;
using ServiceLib.Manager;
using ServiceLib.Models.Configs;
using ServiceLib.Models.Entities;
using v2rayN.Web.Contracts;
using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

[NotInParallel]
public class SubscriptionCreationTests
{
    [Test]
    public async Task EmptyUrlSubscriptionIsCreatedAndReturnedWithAnEmptyUrl()
    {
        var previousLocalData = Environment.GetEnvironmentVariable(Global.LocalAppData);
        var manager = AppManager.Instance;
        var configField = typeof(AppManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AppManager).FullName, "_config");
        var previousConfig = configField.GetValue(manager);
        var config = new Config();
        SubItem? saved = null;
        try
        {
            Environment.SetEnvironmentVariable(Global.LocalAppData, "0");
            configField.SetValue(manager, config);
            SQLiteHelper.Instance.CreateTable<SubItem>();
            var runtime = new V2rayRuntime(null!, null!, null!, null!, null!);
            var result = await runtime.AddSubscriptionAsync(new SubscriptionInput(
                "自建", string.Empty, MoreUrl: string.Empty, Enabled: false, AutoUpdateInterval: 0));

            if (result.Data is not null)
            {
                saved = await manager.GetSubItem(result.Data.Id);
            }

            await result.Success.Should().BeTrue();
            await (result.Data is not null).Should().BeTrue();
            await result.Data!.Url.Should().BeEqualTo(string.Empty);

            await (saved is not null).Should().BeTrue();
            await saved!.Remarks.Should().BeEqualTo("自建");
            await saved.Url.Should().BeEqualTo(string.Empty);
            await saved.MoreUrl.Should().BeEqualTo(string.Empty);
            await saved.Enabled.Should().BeFalse();
            await saved.AutoUpdateInterval.Should().BeEqualTo(0);

            var updateResult = await runtime.UpdateSubscriptionAsync(result.Data!.Id, new SubscriptionInput(
                "自建（已编辑）", string.Empty, MoreUrl: string.Empty, Enabled: true, AutoUpdateInterval: 0));
            await updateResult.Success.Should().BeTrue();
            await updateResult.Data!.Url.Should().BeEqualTo(string.Empty);

            saved = await manager.GetSubItem(result.Data.Id);
            await (saved is not null).Should().BeTrue();
            await saved!.Remarks.Should().BeEqualTo("自建（已编辑）");
            await saved.Url.Should().BeEqualTo(string.Empty);
            await saved.Enabled.Should().BeTrue();
            await saved.AutoUpdateInterval.Should().BeEqualTo(0);

            var reloadedView = V2rayRuntime.ToSubscriptionView(saved);
            await reloadedView.Url.Should().BeEqualTo(string.Empty);
            await reloadedView.Enabled.Should().BeTrue();
            await reloadedView.AutoUpdateInterval.Should().BeEqualTo(0);
        }
        finally
        {
            try
            {
                if (saved is not null)
                {
                    await SQLiteHelper.Instance.DeleteAsync(saved);
                }
            }
            finally
            {
                configField.SetValue(manager, previousConfig);
                Environment.SetEnvironmentVariable(Global.LocalAppData, previousLocalData);
            }
        }
    }
}
