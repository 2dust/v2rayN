using v2rayN.Web.Services;

namespace v2rayN.Web.Tests;

public class XrayGeoStagingTests
{
    [Test]
    public async Task ApplyCopiesTheLatestGeoFilesAfterStaging()
    {
        var root = Path.Combine(Path.GetTempPath(), $"v2rayn-web-xray-stage-{Guid.NewGuid():N}");
        var installPath = Path.Combine(root, "installed");
        var stagingPath = Path.Combine(root, "staging");
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(stagingPath);

        try
        {
            var installedGeo = Path.Combine(installPath, "geoip.dat");
            var stagedGeo = Path.Combine(stagingPath, "geoip.dat");
            await File.WriteAllTextAsync(installedGeo, "old geo data");
            await File.WriteAllTextAsync(Path.Combine(stagingPath, "xray"), "staged executable");

            await File.Exists(stagedGeo).Should().BeFalse();

            // A Geo update completes after Xray staging but before exclusive apply.
            await File.WriteAllTextAsync(installedGeo, "latest geo data");
            V2rayRuntime.CopyLatestGeoFilesForApply(installPath, stagingPath);

            await (await File.ReadAllTextAsync(stagedGeo)).Should().BeEqualTo("latest geo data");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
