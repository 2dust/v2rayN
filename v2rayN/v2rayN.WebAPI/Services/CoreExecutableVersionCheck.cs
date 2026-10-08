using System.Text.RegularExpressions;

namespace v2rayN.WebAPI.Services;

internal static class CoreExecutableVersionCheck
{
    private static readonly Regex VersionPattern = new(
        @"\b[vV]?\d+\.\d+\.\d+\b",
        RegexOptions.CultureInvariant);

    public static bool IsValid(string? versionOutput, string expectedName) =>
        !string.IsNullOrWhiteSpace(versionOutput)
        && versionOutput.Contains(expectedName, StringComparison.OrdinalIgnoreCase)
        && VersionPattern.IsMatch(versionOutput);
}
