namespace ServiceLib.Services.AppRouting;

internal sealed class AppRouteMatcher
{
    private readonly Dictionary<string, AppRouteRule> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppRouteRule> _names = new(StringComparer.OrdinalIgnoreCase);
    public bool IncludesChildren => _paths.Values.Concat(_names.Values).Any(rule => rule.IncludeChildProcesses);

    public bool NeedsPath(string name) => _paths.Keys.Any(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));

    public AppRouteMatcher(IEnumerable<AppRouteRule> rules)
    {
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            (rule.MatchByName ? _names : _paths).Add(Normalize(rule.ExecutablePath, rule.MatchByName), rule);
        }
    }

    public static string Normalize(string value, bool matchByName)
    {
        var executable = value.Trim();
        if (executable.Length >= 2 && executable[0] == '"' && executable[^1] == '"')
        {
            executable = executable[1..^1];
        }

        if (matchByName)
        {
            // A path selected in the browser/picker may also be used to create a name rule.
            executable = Path.GetFileName(executable);
            if (executable.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || executable.Length == 0)
            {
                throw new ArgumentException(ResUI.AppRoutingInvalidExecutable);
            }
        }
        else
        {
            if (!Path.IsPathFullyQualified(executable))
            {
                throw new ArgumentException(ResUI.AppRoutingInvalidExecutable);
            }

            executable = Path.GetFullPath(executable);
        }
        if (!string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(ResUI.AppRoutingInvalidExecutable);
        }

        return executable;
    }

    public AppRouteRule? Find(string? path, string executableName)
    {
        if (!string.IsNullOrEmpty(path) && _paths.TryGetValue(Path.GetFullPath(path), out var exact))
        {
            return exact;
        }

        return _names.GetValueOrDefault(executableName);
    }
}
