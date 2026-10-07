using v2rayN.WebAPI.Launcher;

namespace v2rayN.WebAPI.Tests;

public class NativeWebUpdateHelperTests
{
    [Test]
    public async Task OnlyTheInstalledExecutableAndGuidNamedWorkersAreAccepted()
    {
        await NativeWebUpdateHelper.IsUpdateHelperExecutableName("v2rayN.WebAPI").Should().BeTrue();
        await NativeWebUpdateHelper.IsUpdateHelperExecutableName(
            ".v2rayn-web-update-helper-" + Guid.NewGuid().ToString("N")).Should().BeTrue();
        foreach (var name in new[] { "", "v2rayN.Web", "other", ".v2rayn-web-update-helper-",
            ".v2rayn-web-update-helper-not-a-guid", ".v2rayn-web-update-helper-" + Guid.NewGuid().ToString("D"),
            "../v2rayN.WebAPI", ".v2rayn-web-update-helper-" + Guid.NewGuid().ToString("N") + ".bak" })
        {
            await NativeWebUpdateHelper.IsUpdateHelperExecutableName(name).Should().BeFalse();
        }
    }

    [Test]
    public async Task WorkerBundleStaysBesideTheInstallAndSurvivesExecutableReplacement()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"webapi-helper-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var executable = Path.Combine(directory, "v2rayN.WebAPI");
            await File.WriteAllTextAsync(executable, "original-bundle");
            var helper = NativeWebUpdateHelper.CreateIsolatedHelperExecutable(executable);
            await Path.GetDirectoryName(helper).Should().BeEqualTo(directory);
            await NativeWebUpdateHelper.IsUpdateHelperExecutableName(Path.GetFileName(helper)).Should().BeTrue();
            if (OperatingSystem.IsLinux())
                await File.GetUnixFileMode(helper).Should().BeEqualTo(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var candidate = Path.Combine(directory, "candidate");
            await File.WriteAllTextAsync(candidate, "different-bundle-layout");
            File.Move(candidate, executable, overwrite: true);
            await (await File.ReadAllTextAsync(helper)).Should().BeEqualTo("original-bundle");
            await (await File.ReadAllTextAsync(executable)).Should().BeEqualTo("different-bundle-layout");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SymlinkManagedExecutablesCannotCreateWorkers()
    {
        if (!OperatingSystem.IsLinux()) return;
        var directory = Path.Combine(Path.GetTempPath(), $"webapi-helper-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "target");
            await File.WriteAllTextAsync(target, "unchanged");
            var link = Path.Combine(directory, "v2rayN.WebAPI");
            File.CreateSymbolicLink(link, target);
            var rejected = false;
            try { NativeWebUpdateHelper.CreateIsolatedHelperExecutable(link); }
            catch (InvalidDataException) { rejected = true; }
            await rejected.Should().BeTrue();
            await Directory.GetFiles(directory, ".v2rayn-web-update-helper-*").Length.Should().BeEqualTo(0);
            await (await File.ReadAllTextAsync(target)).Should().BeEqualTo("unchanged");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
