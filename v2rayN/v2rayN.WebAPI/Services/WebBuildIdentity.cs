using System.Reflection;
using System.Runtime.InteropServices;

namespace v2rayN.WebAPI.Services;

public sealed record WebBuildIdentity(string Version, string Commit, string BuildDate, string Rid, string Repository)
{
    public static WebBuildIdentity Current { get; } = Load();

    private static WebBuildIdentity Load()
    {
        var metadata = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(item => item.Key is "WebVersion" or "WebCommit" or "WebBuildDate" or "WebRepository")
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        var repository = metadata.GetValueOrDefault("WebRepository");
        if (!WebReleaseChannel.IsValidRepository(repository))
            repository = WebReleaseChannel.DefaultRepository;
        return new WebBuildIdentity(
            metadata.GetValueOrDefault("WebVersion") ?? "0.0.0-dev",
            metadata.GetValueOrDefault("WebCommit") ?? "unknown",
            metadata.GetValueOrDefault("WebBuildDate") ?? "unknown",
            RuntimeInformation.RuntimeIdentifier,
            repository!);
    }
}
