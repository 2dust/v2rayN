using ServiceLib.Tests.CoreConfig;
using ServiceLib.ViewModels;
using ReactiveUI.Builder;

namespace ServiceLib.Tests.Manager;

[NotInParallel]
public class SubscriptionSelectionTests
{
    [Test]
    public async Task OrdinaryListRefresh_AfterActiveProfileRemoval_DoesNotChooseAnotherServer()
    {
        ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        SQLiteHelper.Instance.CreateTable<SubItem>();
        config.SubIndexId = $"selection-{Guid.NewGuid():N}";
        config.IndexId = $"removed-{Guid.NewGuid():N}";
        var selected = config.IndexId;
        var replacement = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, $"replacement-{Guid.NewGuid():N}");
        replacement.Subid = config.SubIndexId;
        await SQLiteHelper.Instance.ReplaceAsync(replacement);
        var field = typeof(CoreManager).GetField("_runningConfig", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var previous = field.GetValue(CoreManager.Instance);
        var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, selected);
        field.SetValue(CoreManager.Instance, await PreparedCoreConfigBundle.CreateAsync(CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray), null));
        try
        {
            var viewModel = new ProfilesViewModel();
            await viewModel.RefreshServersBiz();
            await config.IndexId.Should().BeEqualTo(selected);
        }
        finally
        {
            field.SetValue(CoreManager.Instance, previous);
            await SQLiteHelper.Instance.DeleteAsync(replacement);
        }
    }
}
