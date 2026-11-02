namespace ServiceLib.Tests.CoreConfig.Context;

[NotInParallel]
public class SubscriptionApplyPolicyTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExistingAcceptedWarning_DoesNotBlockChangedCredentials(bool insecureTls)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        EnsureTables();
        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray, $"warning-{Guid.NewGuid():N}");
        if (insecureTls)
        {
            node.StreamSecurity = Global.StreamSecurity;
            node.AllowInsecure = "true";
        }
        else
        {
            node.Network = nameof(ETransport.ws);
            node.Alpn = "h2";
        }
        var before = await CoreConfigContextBuilder.BuildAll(config, node);
        await before.Success.Should().BeTrue();
        await (before.CombinedValidatorResult.Warnings.Count > 0).Should().BeTrue();
        await before.CanApplySubscriptionUpdate.Should().BeTrue();
        var running = await PreparedCoreConfigBundle.CreateAsync(before.MainResult.Context, null);
        node.Password = "00000000-0000-0000-0000-000000000002";
        var after = await CoreConfigContextBuilder.BuildAll(config, node);
        await after.CanApplySubscriptionUpdate.Should().BeTrue();
        var changed = await PreparedCoreConfigBundle.CreateAsync(after.MainResult.Context, null);
        await changed.IsEquivalentTo(running).Should().BeFalse();
    }

    [Test]
    public async Task MissingSubscriptionChainDependency_DefersAutomaticApplication()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        EnsureTables();
        var sub = new SubItem { Id = $"dependency-{Guid.NewGuid():N}", PrevProfile = $"missing-{Guid.NewGuid():N}" };
        await SQLiteHelper.Instance.ReplaceAsync(sub);
        try
        {
            var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.Xray, $"node-{Guid.NewGuid():N}");
            node.Subid = sub.Id;
            var result = await CoreConfigContextBuilder.BuildAll(config, node);
            await result.Success.Should().BeTrue();
            await result.CanApplySubscriptionUpdate.Should().BeFalse();
            await result.CombinedValidatorResult.HasUnresolvedDependencies.Should().BeTrue();
        }
        finally { await SQLiteHelper.Instance.DeleteAsync(sub); }
    }

    private static void EnsureTables()
    {
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<RoutingItem>();
        SQLiteHelper.Instance.CreateTable<DNSItem>();
        SQLiteHelper.Instance.CreateTable<FullConfigTemplateItem>();
    }
}
