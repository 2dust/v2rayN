namespace ServiceLib.CubeAuth;

public class CubeAuthUser
{
    public string Id { get; set; } = "";
    public string Identifier { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";
}

public class CubeAccountService
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    [JsonPropertyName("subscription_url")]
    public string SubscriptionUrl { get; set; } = "";

    public long Expire { get; set; }

    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; set; }

    [JsonPropertyName("used_bytes")]
    public long UsedBytes { get; set; }
}

/// <summary>Raw shape of every CubeVPN API response (see docs/api-contract.md).</summary>
internal class CubeApiEnvelope
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }

    [JsonPropertyName("cooldown_seconds")]
    public int CooldownSeconds { get; set; } = 60;

    public CubeAuthUser? User { get; set; }
    public List<CubeAccountService>? Services { get; set; }
}

public abstract record CubeAuthResult
{
    public sealed record RequestCodeOk(int CooldownSeconds) : CubeAuthResult;

    public sealed record VerifyOk(string Token, CubeAuthUser User) : CubeAuthResult;

    public sealed record AccountOk(CubeAuthUser User, List<CubeAccountService> Services) : CubeAuthResult;

    public sealed record Error(string Code, string Message) : CubeAuthResult;
}
