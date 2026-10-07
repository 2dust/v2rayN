using v2rayN.WebAPI.Hosting;

namespace v2rayN.WebAPI.Tests;

public class WebUiHostOptionsTests
{
    [Test]
    public async Task UnsetPathDefaultsToWebUiBesideTheApplication()
    {
        var options = WebUiHostOptions.Resolve(AppContext.BaseDirectory, configuredPath: null);

        await options.RootPath.Should().BeEqualTo(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "webui")));
        await options.ConfigurationError.Should().BeNull();
    }

    [Test]
    public async Task AbsolutePathIsNormalizedWithoutChangingItsBase()
    {
        using var directory = new TemporaryDirectory();
        var expected = Path.Combine(directory.Path, "selected-ui");

        var options = WebUiHostOptions.Resolve("/ignored/application", expected);

        await options.RootPath.Should().BeEqualTo(Path.GetFullPath(expected));
    }

    [Test]
    public async Task RelativePathIsResolvedFromApplicationBaseNotCurrentDirectory()
    {
        using var directory = new TemporaryDirectory();
        const string relative = "custom-ui/site";

        var options = WebUiHostOptions.Resolve(directory.Path, relative);

        await options.RootPath.Should().BeEqualTo(Path.GetFullPath(Path.Combine(directory.Path, relative)));
    }

    [Test]
    public async Task ExplicitlyEmptyPathDisablesStaticHosting()
    {
        var options = WebUiHostOptions.Resolve(AppContext.BaseDirectory, string.Empty);

        await options.RootPath.Should().BeNull();
        await options.IsAvailable.Should().BeFalse();
    }

    [Test]
    public async Task MalformedPathFailsSafeByDisablingStaticHosting()
    {
        var options = WebUiHostOptions.Resolve(AppContext.BaseDirectory, "\0invalid");

        await options.RootPath.Should().BeNull();
        await options.ConfigurationError.Should().NotBeNull();
        await options.IsAvailable.Should().BeFalse();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-webui-options-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
