using System.Text.RegularExpressions;

namespace v2rayN.WebAPI.Services;

/// <summary>
/// Describes the official v2rayN release channel used by the Web self-update.
/// Official releases use the upstream release tag (for example <c>7.25.3</c>) and carry
/// <c>web-update.json</c> plus the app-only ZIP archives as additional release assets.
/// </summary>
internal static partial class WebReleaseChannel
{
    public const string DefaultRepository = "2dust/v2rayN";
    public const string ManifestAssetName = "web-update.json";
    private const string ApiBase = "https://api.github.com/repos";

    public static bool IsValidRepository(string? value) =>
        !string.IsNullOrWhiteSpace(value) && RepositoryPattern().IsMatch(value);

    public static string BuildReleaseIndexUrl(string repository)
    {
        if (!IsValidRepository(repository))
            throw new ArgumentException("The release repository identifier is invalid.", nameof(repository));
        return $"{ApiBase}/{repository}/releases?per_page=100";
    }

    public static bool TryParseReleaseTag(string? tag, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(tag) || !ReleaseTagPattern().IsMatch(tag)) return false;
        version = tag;
        return true;
    }

    public static bool IsTrustedAssetUrl(string? value, string repository, string tag, string assetName)
    {
        if (!IsValidRepository(repository)
            || string.IsNullOrWhiteSpace(tag)
            || string.IsNullOrWhiteSpace(assetName)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Host != "github.com")
        {
            return false;
        }
        var expectedPath = $"/{repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(assetName)}";
        return uri.AbsolutePath.Equals(expectedPath, StringComparison.Ordinal);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryPattern();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseTagPattern();
}
