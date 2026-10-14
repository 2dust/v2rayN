namespace ServiceLib.Tests.Manager;

public class CoreAdminManagerTests
{
    [Test]
    public async Task CheckLinuxSudoPassword_NullOrEmpty_ShouldReturnFalse()
    {
        var resultNull = await CoreAdminManager.CheckLinuxSudoPassword(null);
        await resultNull.Should().BeFalse();

        var resultEmpty = await CoreAdminManager.CheckLinuxSudoPassword(string.Empty);
        await resultEmpty.Should().BeFalse();
    }

    [Test]
    public async Task CheckLinuxSudoPassword_OnWindows_ShouldReturnFalseWithoutExecuting()
    {
        if (Utils.IsWindows())
        {
            var result = await CoreAdminManager.CheckLinuxSudoPassword("somePassword123");
            await result.Should().BeFalse();
        }
    }
}
