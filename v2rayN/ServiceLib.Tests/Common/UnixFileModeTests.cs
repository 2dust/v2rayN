namespace ServiceLib.Tests.Common;

public class UnixFileModeTests
{
    [Test]
    public async Task AlreadyExecutableSystemBinary_ShouldNotRequireOwnership()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // /bin/sh is executable but owned by root on the Unix CI runners.
        // A regular user must not need chmod permission just to launch it.
        var mode = File.GetUnixFileMode("/bin/sh");
        await Utils.SetUnixFileMode("/bin/sh").Should().BeTrue();
        await File.GetUnixFileMode("/bin/sh").Should().BeEqualTo(mode);
    }

    [Test]
    public async Task NonExecutableOwnedFile_ShouldGainExecuteBitsWithoutLosingOtherPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.GetTempFileName();
        try
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
            File.SetUnixFileMode(path, mode);

            await Utils.SetUnixFileMode(path).Should().BeTrue();
            await File.GetUnixFileMode(path).Should().BeEqualTo(mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
