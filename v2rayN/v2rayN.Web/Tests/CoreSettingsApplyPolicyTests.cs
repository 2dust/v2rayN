using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class CoreSettingsApplyPolicyTests
{
    [Test]
    public async Task ApplyPolicyDistinguishesStoppedRunningFaultedAndTransitionStates()
    {
        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Stopped, hasActiveChild: false, changed: true)
            .Should().BeEqualTo(CoreSettingsApplyAction.SaveOnly);
        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Running, hasActiveChild: true, changed: true)
            .Should().BeEqualTo(CoreSettingsApplyAction.Restart);
        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Faulted, hasActiveChild: true, changed: true)
            .Should().BeEqualTo(CoreSettingsApplyAction.Restart);
        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Faulted, hasActiveChild: false, changed: true)
            .Should().BeEqualTo(CoreSettingsApplyAction.SaveOnly);

        foreach (var transition in new[]
                 {
                     CoreRuntimeState.Starting,
                     CoreRuntimeState.Stopping,
                     CoreRuntimeState.Restarting,
                 })
        {
            await CoreSettingsApplyPolicy.Decide(transition, hasActiveChild: true, changed: true)
                .Should().BeEqualTo(CoreSettingsApplyAction.Busy);
        }

        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Running, hasActiveChild: true, changed: false)
            .Should().BeEqualTo(CoreSettingsApplyAction.SaveOnly);
    }

    [Test]
    public async Task AReportedRunningRuntimeWithoutAChildIsNotTreatedAsStopped()
    {
        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Running, hasActiveChild: false, changed: true)
            .Should().BeEqualTo(CoreSettingsApplyAction.Inconsistent);
        await CoreSettingsApplyPolicy.Decide(CoreRuntimeState.Stopped, hasActiveChild: true, changed: true)
            .Should().BeEqualTo(CoreSettingsApplyAction.Inconsistent);
    }
}
