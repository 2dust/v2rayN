using v2rayN.Web.Configuration;

namespace v2rayN.Web.Tests;

public class NativeEnvironmentFileTests
{
    [Test]
    public async Task MissingFileIsOptionalAndBlankOrCommentOnlyFilesHaveNoEntries()
    {
        using var directory = new TemporaryDirectory();
        NativeEnvironmentFile.LoadFromExecutableDirectory(directory.Path);

        await NativeEnvironmentFile.Parse(string.Empty).Count.Should().BeEqualTo(0);
        await NativeEnvironmentFile.Parse("\n  # comment\n\t\n# another comment\n").Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task ParserAcceptsPlainAndQuotedValuesWithSpacesEqualsAndHashes()
    {
        var parsed = NativeEnvironmentFile.Parse("""
            PLAIN=value with spaces = signs # kept as data
            DOUBLE="  value with spaces = signs # kept  "
            SINGLE='single quoted value = # data'
            EMPTY=
            INLINE_HASH=value#not-a-comment
            """);

        await parsed["PLAIN"].Should().BeEqualTo("value with spaces = signs # kept as data");
        await parsed["DOUBLE"].Should().BeEqualTo("  value with spaces = signs # kept  ");
        await parsed["SINGLE"].Should().BeEqualTo("single quoted value = # data");
        await parsed["EMPTY"].Should().BeEqualTo(string.Empty);
        await parsed["INLINE_HASH"].Should().BeEqualTo("value#not-a-comment");
    }

    [Test]
    public async Task DuplicateKeysUseFirstOccurrence()
    {
        var parsed = NativeEnvironmentFile.Parse("VALUE=first\nVALUE=second\n");

        await parsed["VALUE"].Should().BeEqualTo("first");
    }

    [Test]
    public async Task ProcessEnvironmentOverridesDotEnvIncludingAnEmptyValue()
    {
        var dotenv = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TEST_VALUE"] = "from-dotenv",
            ["EMPTY_VALUE"] = "from-dotenv",
            ["DOTENV_ONLY"] = "from-dotenv",
        };
        var process = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TEST_VALUE"] = "from-process",
            ["EMPTY_VALUE"] = string.Empty,
        };

        var merged = NativeEnvironmentFile.Merge(dotenv, process);

        await merged["TEST_VALUE"].Should().BeEqualTo("from-process");
        await merged["EMPTY_VALUE"].Should().BeEqualTo(string.Empty);
        await merged["DOTENV_ONLY"].Should().BeEqualTo("from-dotenv");
    }

    [Test]
    public async Task LoaderUsesTheProvidedExecutableDirectoryRatherThanAnotherDirectory()
    {
        using var directory = new TemporaryDirectory();
        var executableDirectory = Path.Combine(directory.Path, "install");
        var workingDirectory = Path.Combine(directory.Path, "other-working-directory");
        Directory.CreateDirectory(executableDirectory);
        Directory.CreateDirectory(workingDirectory);
        var variable = $"V2RAYN_DOTENV_PATH_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, null);
        await File.WriteAllTextAsync(Path.Combine(executableDirectory, ".env"), $"{variable}=from-executable-directory\n");
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, ".env"), $"{variable}=from-working-directory\n");

        try
        {
            NativeEnvironmentFile.LoadFromExecutableDirectory(executableDirectory);
            await Environment.GetEnvironmentVariable(variable).Should().BeEqualTo("from-executable-directory");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Test]
    public async Task InvalidKeysQuotesNulShellSyntaxAndOversizedInputsAreRejected()
    {
        foreach (var invalid in new[]
        {
            "1BAD=value",
            "export KEY=value",
            "KEY=\"unterminated",
            "KEY='mismatched\"",
            "KEY=bare\"quote",
            "KEY=first line\nsecond line",
            "KEY=\"first line\nsecond line\"",
            "KEY=value\0hidden",
            "KEY=$(command)",
            "KEY=${OTHER_VAR}",
            "KEY=$OTHER_VAR",
            "KEY=`command`",
            "KEY=back\\slash",
        })
        {
            await IsRejected(invalid).Should().BeTrue();
        }

        await IsRejected(new string('x', NativeEnvironmentFile.MaximumFileBytes + 1)).Should().BeTrue();
        await IsRejected($"VALUE={new string('x', NativeEnvironmentFile.MaximumValueBytes + 1)}").Should().BeTrue();
    }

    [Test]
    public async Task MalformedFileErrorIdentifiesLineWithoutEchoingItsContents()
    {
        var secretLikeValue = "KEY=private-malformed-value\"";
        var message = string.Empty;
        try
        {
            _ = NativeEnvironmentFile.Parse($"# safe comment\n{secretLikeValue}\n");
        }
        catch (NativeEnvironmentConfigurationException exception)
        {
            message = exception.Message;
        }

        await message.Should().Contain("Line 2");
        await message.Should().NotContain("private-malformed-value");
    }

    private static bool IsRejected(string value)
    {
        try
        {
            _ = NativeEnvironmentFile.Parse(value);
            return false;
        }
        catch (NativeEnvironmentConfigurationException)
        {
            return true;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"v2rayn-web-dotenv-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
