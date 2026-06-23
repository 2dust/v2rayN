using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Common;

public class FileUtilsTests
{
    [Fact]
    public async Task CreateLinuxShellFile_ShouldMakeExistingFileExecutable_WhenOverwriteIsFalse()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var fileName = $"test-shell-{Guid.NewGuid():N}.sh";
        var filePath = Utils.GetBinConfigPath(fileName);

        try
        {
            File.WriteAllText(filePath, "old content");
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            var result = await FileUtils.CreateLinuxShellFile(fileName, "new content", false);

            result.Should().Be(filePath);
            File.ReadAllText(filePath).Should().Be("old content");

            var mode = File.GetUnixFileMode(filePath);
            mode.Should().HaveFlag(UnixFileMode.UserExecute);
            mode.Should().HaveFlag(UnixFileMode.GroupExecute);
            mode.Should().HaveFlag(UnixFileMode.OtherExecute);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
