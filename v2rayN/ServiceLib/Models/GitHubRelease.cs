using System.Text.Json.Serialization;

namespace ServiceLib.Models
{
	public class GitHubReleaseAsset
	{
		[JsonPropertyName("url")] public string? Url { get; set; }

		[JsonPropertyName("id")] public int Id { get; set; }

		[JsonPropertyName("node_id")] public string? NodeId { get; set; }

		[JsonPropertyName("name")] public string? Name { get; set; }

		[JsonPropertyName("label")] public object Label { get; set; }

		[JsonPropertyName("content_type")] public string? ContentType { get; set; }

		[JsonPropertyName("state")] public string? State { get; set; }

		[JsonPropertyName("size")] public int Size { get; set; }

		[JsonPropertyName("download_count")] public int DownloadCount { get; set; }

		[JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }

		[JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }

		[JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
	}

	public class GitHubRelease
	{
		[JsonPropertyName("url")] public string? Url { get; set; }

		[JsonPropertyName("assets_url")] public string? AssetsUrl { get; set; }

		[JsonPropertyName("upload_url")] public string? UploadUrl { get; set; }

		[JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }

		[JsonPropertyName("id")] public int Id { get; set; }

		[JsonPropertyName("node_id")] public string? NodeId { get; set; }

		[JsonPropertyName("tag_name")] public string? TagName { get; set; }

		[JsonPropertyName("target_commitish")] public string? TargetCommitish { get; set; }

		[JsonPropertyName("name")] public string? Name { get; set; }

		[JsonPropertyName("draft")] public bool Draft { get; set; }

		[JsonPropertyName("prerelease")] public bool Prerelease { get; set; }

		[JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }

		[JsonPropertyName("published_at")] public DateTime PublishedAt { get; set; }

		[JsonPropertyName("assets")] public List<GitHubReleaseAsset> Assets { get; set; }

		[JsonPropertyName("tarball_url")] public string? TarballUrl { get; set; }

		[JsonPropertyName("zipball_url")] public string? ZipballUrl { get; set; }

		[JsonPropertyName("body")] public string? Body { get; set; }
	}
}
