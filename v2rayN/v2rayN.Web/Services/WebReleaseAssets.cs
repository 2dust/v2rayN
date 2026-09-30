using System.Text.Json;

namespace v2rayN.Web.Services;

/// <summary>
/// The single source of truth for v2rayN Web release asset names.
/// <c>Assets/web-assets.json</c> is embedded here and read by the packaging and manifest
/// scripts, so the updater, the manifest, and the release ZIPs never disagree about names.
/// </summary>
internal static class WebReleaseAssets
{
    private const string ResourceName = "v2rayN.Web.Assets.web-assets.json";
    private static readonly IReadOnlyDictionary<string, Entry> Map = Load();

    public static string? ArtifactArch(string? rid) => Find(rid)?.Arch;

    public static string? FullAssetName(string? rid) => Find(rid)?.Full;

    public static string? UpdateAssetName(string? rid) => Find(rid)?.Update;

    private static Entry? Find(string? rid) =>
        rid is not null && Map.TryGetValue(rid, out var entry) ? entry : null;

    private sealed record Entry(string Arch, string Full, string Update);

    private static IReadOnlyDictionary<string, Entry> Load()
    {
        using var stream = typeof(WebReleaseAssets).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"The embedded Web release asset map is missing: {ResourceName}");
        using var document = JsonDocument.Parse(stream);
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var element in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            var rid = element.GetProperty("rid").GetString();
            var arch = element.GetProperty("arch").GetString();
            var full = element.GetProperty("full").GetString();
            var update = element.GetProperty("update").GetString();
            if (string.IsNullOrWhiteSpace(rid)
                || string.IsNullOrWhiteSpace(arch)
                || string.IsNullOrWhiteSpace(full)
                || string.IsNullOrWhiteSpace(update)
                || !map.TryAdd(rid, new Entry(arch, full, update)))
            {
                throw new InvalidDataException("The embedded Web release asset map is invalid.");
            }
        }
        return map;
    }
}
