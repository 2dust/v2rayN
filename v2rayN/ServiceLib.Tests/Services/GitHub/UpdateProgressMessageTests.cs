namespace ServiceLib.Tests.Services.GitHub;

public class UpdateProgressMessageTests
{
    [Test]
    public async Task UpdateProgressMessages_AreResolvableForEveryStage()
    {
        var messages = new[]
        {
            string.Format(ResUI.MsgCheckUpdateViaLatest, ECoreType.Xray),
            string.Format(ResUI.MsgCheckUpdateViaApi, ECoreType.Xray),
            string.Format(ResUI.MsgCheckUpdateViaApiFailed, ECoreType.Xray),
            string.Format(ResUI.MsgCheckUpdateViaGitRefs, ECoreType.Xray),
            string.Format(ResUI.MsgCheckUpdateViaGitRefsFailed, ECoreType.Xray),
        };

        foreach (var message in messages)
        {
            await message.Contains("Xray").Should().BeTrue();
        }
    }
}
