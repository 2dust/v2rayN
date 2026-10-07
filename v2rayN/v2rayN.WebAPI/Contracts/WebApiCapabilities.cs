namespace v2rayN.WebAPI.Contracts;

public static class WebApiCapabilities
{
    public static string[] Current =>
    [
        "auth.sessions",
        "events.sse",
        "editor.options",
        "profiles",
        "subscriptions",
        "routing",
        "dns",
        "settings",
        "backup.restore",
        "core.runtime",
        "core.updates",
        "web.self-update",
        "static-webui",
    ];
}
