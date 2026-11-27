namespace ServiceLib.Services.GitHub;

/// <summary>
///     Extracts tag names from the pkt-line payload of the git info/refs endpoint.
/// </summary>
public static class GitHubRefsParser
{
    private const string TargetRefPrefix = "refs/tags/";

    public static List<string> ParseTags(string? content)
    {
        var tags = new List<string>();
        if (content.IsNullOrEmpty())
        {
            return tags;
        }

        var remaining = content.AsSpan();
        ReadOnlySpan<char> targetRefSpan = TargetRefPrefix;

        while (!remaining.IsEmpty)
        {
            var newlineIndex = remaining.IndexOf('\n');
            var line = newlineIndex >= 0
                ? remaining[..newlineIndex]
                : remaining;

            remaining = newlineIndex >= 0
                ? remaining[(newlineIndex + 1)..]
                : ReadOnlySpan<char>.Empty;

            line = line.TrimEnd('\r');

            // flush packet "0000" has no trailing newline, so the next pkt length is glued to this line
            if (line.StartsWith("0000", StringComparison.Ordinal))
            {
                line = line[4..];
            }

            // Remove pkt-line length prefix (4 hex digits)
            if (line.Length <= 4)
            {
                continue;
            }

            var payload = line[4..];
            var spaceIndex = payload.IndexOf(' ');
            if (spaceIndex <= 0)
            {
                continue;
            }

            var refSpan = payload[(spaceIndex + 1)..].Trim();

            // strip the capability list and skip peeled annotated tags
            var nulIndex = refSpan.IndexOf('\0');
            if (nulIndex >= 0)
            {
                refSpan = refSpan[..nulIndex].TrimEnd();
            }
            if (refSpan.EndsWith("^{}", StringComparison.Ordinal))
            {
                continue;
            }

            if (refSpan.StartsWith(targetRefSpan, StringComparison.Ordinal))
            {
                tags.Add(refSpan[targetRefSpan.Length..].ToString());
            }
        }

        return tags;
    }
}
