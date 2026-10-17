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

    [Test]
    public async Task IsCorePrivileged_NullOrNonExistent_ShouldReturnFalse()
    {
        await CoreAdminManager.IsCorePrivileged(null).Should().BeFalse();
        await CoreAdminManager.IsCorePrivileged(string.Empty).Should().BeFalse();
        await CoreAdminManager.IsCorePrivileged("non_existent_file_path_12345").Should().BeFalse();
    }

    [Test]
    public async Task IsCorePrivileged_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                await CoreAdminManager.IsCorePrivileged(tempFile).Should().BeFalse();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task AreAllTunCoresPrivileged_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            await CoreAdminManager.AreAllTunCoresPrivileged().Should().BeFalse();
        }
    }

    [Test]
    public async Task IsCorePrivilegedAsync_NullOrNonExistent_ShouldReturnFalse()
    {
        var r1 = await CoreAdminManager.IsCorePrivilegedAsync(null);
        await r1.Should().BeFalse();
        var r2 = await CoreAdminManager.IsCorePrivilegedAsync(string.Empty);
        await r2.Should().BeFalse();
        var r3 = await CoreAdminManager.IsCorePrivilegedAsync("non_existent_file_path_12345");
        await r3.Should().BeFalse();
    }

    [Test]
    public async Task IsCorePrivilegedAsync_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var result = await CoreAdminManager.IsCorePrivilegedAsync(tempFile);
                await result.Should().BeFalse();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task AreAllTunCoresPrivilegedAsync_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            var result = await CoreAdminManager.AreAllTunCoresPrivilegedAsync();
            await result.Should().BeFalse();
        }
    }

    [Test]
    public async Task GrantAllCoresPrivilege_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            var result = await CoreAdminManager.GrantAllCoresPrivilege("somePassword");
            await result.Should().BeFalse();
        }
    }

    [Test]
    public async Task RevokeAllCoresPrivilege_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            var result = await CoreAdminManager.RevokeAllCoresPrivilege("somePassword");
            await result.Should().BeFalse();
        }
    }

    [Test]
    public async Task IsAnyCorePrivileged_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            await CoreAdminManager.IsAnyCorePrivileged().Should().BeFalse();
        }
    }

    [Test]
    public async Task IsAnyCorePrivilegedAsync_OnWindows_ShouldReturnFalse()
    {
        if (Utils.IsWindows())
        {
            var result = await CoreAdminManager.IsAnyCorePrivilegedAsync();
            await result.Should().BeFalse();
        }
    }

    [Test]
    public async Task GetAllCoreExecFiles_ShouldReturnNonNullList()
    {
        var files = CoreAdminManager.GetAllCoreExecFiles();
        await files.Should().NotBeNull();
    }
}
