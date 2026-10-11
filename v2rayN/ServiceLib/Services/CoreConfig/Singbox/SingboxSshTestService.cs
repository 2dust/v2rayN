namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigSingboxService
{
    /// <summary>Draft-only probe: reuse SSH credential mapping, with no routing/template/database dependencies.</summary>
    public string GenerateSshConnectionTestConfig(int port, string localPassword)
    {
        if (_node.ConfigType != EConfigType.SSH) throw new SshSetupException("configuration");
        var outbound = BuildProxyOutbound();
        return new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "error" },
            ["inbounds"] = new JsonArray(new JsonObject
            {
                ["type"] = "mixed", ["listen"] = Global.Loopback, ["listen_port"] = port,
                ["users"] = new JsonArray(new JsonObject { ["username"] = "probe", ["password"] = localPassword }),
            }),
            ["outbounds"] = new JsonArray(JsonNode.Parse(JsonUtils.Serialize(outbound))),
            ["dns"] = new JsonObject
            {
                ["servers"] = new JsonArray(new JsonObject { ["type"] = "local", ["tag"] = "local" }),
            },
            ["route"] = new JsonObject
            {
                ["final"] = Global.ProxyTag,
                ["default_domain_resolver"] = new JsonObject { ["server"] = "local" },
            },
        }.ToJsonString();
    }
}
