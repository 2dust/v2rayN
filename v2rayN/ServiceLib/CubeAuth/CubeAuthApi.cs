namespace ServiceLib.CubeAuth;

/// <summary>
/// Talks to the CubeVPN account API for OTP sign-in via @cubevvpn_bot and
/// fetching the user's purchased-service subscription links. Mirrors the
/// Android app's Auth.kt so both clients speak the same contract.
/// </summary>
public static class CubeAuthApi
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static string Base => CubeApiConfig.BaseUrl.TrimEnd('/');

    public static async Task<CubeAuthResult> RequestCodeAsync(string identifier)
    {
        var env = await SendAsync(HttpMethod.Post, "/api/requestcode.php", new { identifier }, null);
        return env.Ok ? new CubeAuthResult.RequestCodeOk(env.CooldownSeconds) : ErrorFrom(env);
    }

    public static async Task<CubeAuthResult> VerifyCodeAsync(string identifier, string code)
    {
        var env = await SendAsync(HttpMethod.Post, "/api/verifycode.php", new { identifier, code }, null);
        if (!env.Ok)
        {
            return ErrorFrom(env);
        }
        if (env.Token.IsNullOrEmpty() || env.User == null)
        {
            return new CubeAuthResult.Error("bad_response", "Malformed server response");
        }
        var user = env.User;
        if (user.Identifier.IsNullOrEmpty())
        {
            user.Identifier = identifier;
        }
        return new CubeAuthResult.VerifyOk(env.Token, user);
    }

    public static async Task<CubeAuthResult> FetchAccountAsync(string token)
    {
        var env = await SendAsync(HttpMethod.Get, "/api/accountme.php", null, token);
        if (!env.Ok)
        {
            return ErrorFrom(env);
        }
        return new CubeAuthResult.AccountOk(env.User ?? new CubeAuthUser(), env.Services ?? []);
    }

    public static async Task LogoutAsync(string token)
    {
        try
        {
            await SendAsync(HttpMethod.Post, "/api/logout.php", new { }, token);
        }
        catch
        {
            // best effort — the local token is cleared regardless
        }
    }

    private static CubeAuthResult.Error ErrorFrom(CubeApiEnvelope env) =>
        new(env.Error ?? "unknown", env.Message ?? "Request failed");

    /// <summary>Never throws — network/parse failures come back as a synthetic ok:false envelope.</summary>
    private static async Task<CubeApiEnvelope> SendAsync(HttpMethod method, string path, object? body, string? token)
    {
        if (CubeApiConfig.BaseUrl.IsNullOrEmpty())
        {
            return NetworkError("API base URL is not configured in this build");
        }
        try
        {
            using var req = new HttpRequestMessage(method, Base + path);
            if (token != null)
            {
                req.Headers.Add("Authorization", $"Bearer {token}");
            }
            req.Headers.Add("Accept", "application/json");
            if (body != null)
            {
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            using var resp = await _http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (text.IsNullOrEmpty())
            {
                return NetworkError($"server returned HTTP {(int)resp.StatusCode} with an empty body");
            }
            try
            {
                return JsonUtils.Deserialize<CubeApiEnvelope>(text) ?? NetworkError("empty JSON");
            }
            catch (Exception)
            {
                var snippet = text.Length <= 200 ? text : text[..200];
                return NetworkError($"HTTP {(int)resp.StatusCode}, non-JSON response: {snippet}");
            }
        }
        catch (Exception ex)
        {
            return NetworkError($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static CubeApiEnvelope NetworkError(string detail) => new()
    {
        Ok = false,
        Error = "network",
        Message = $"Network error — {detail}",
    };
}
