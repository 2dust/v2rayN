namespace v2rayN.WebAPI.Services;

internal enum RuntimeRequestOperationKind
{
    Shared,
    Observation,
    Exclusive,
    ExclusiveReadOnly,
    Background,
}

internal static class RuntimeRequestOperationPolicy
{
    public static RuntimeRequestOperationKind Classify(string method, string path)
    {
        var normalizedPath = path.TrimEnd('/');
        var isPost = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);
        var isGet = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

        if (isPost
            && (normalizedPath is "/api/core/xray/update" or "/api/core/geo/update" or "/api/web-updates/update" or "/api/speedtests"
                or "/api/subscriptions/update" or "/api/backup/restore" or "/api/backup/webdav/restore"
                || normalizedPath.StartsWith("/api/settings/regional-presets/", StringComparison.Ordinal)
                || normalizedPath.StartsWith("/api/core-updates/", StringComparison.Ordinal)
                    && normalizedPath.EndsWith("/update", StringComparison.Ordinal)
                || (normalizedPath.StartsWith("/api/subscriptions/", StringComparison.Ordinal)
                    && normalizedPath.EndsWith("/update", StringComparison.Ordinal))))
        {
            return RuntimeRequestOperationKind.Background;
        }

        if (isGet
            && normalizedPath is "/api/status" or "/api/operations" or "/api/logs" or "/api/logs/page")
        {
            return RuntimeRequestOperationKind.Observation;
        }

        if (!isGet && normalizedPath.StartsWith("/api/settings/", StringComparison.Ordinal))
        {
            return RuntimeRequestOperationKind.ExclusiveReadOnly;
        }

        if ((isGet && normalizedPath == "/api/backup/download")
            || (isPost && normalizedPath == "/api/backup/webdav"))
        {
            return RuntimeRequestOperationKind.Exclusive;
        }

        return RuntimeRequestOperationKind.Shared;
    }
}
