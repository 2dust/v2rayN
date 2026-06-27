namespace ServiceLib.Models.Dto;

// realm://<token>@<rendezvous-host>[:port]/<realm-name>?stun=<stun-host>[:port]&stun=<stun-host>[:port]...
// realm+http://<token>@<rendezvous-host>[:port]/<realm-name>?stun=<stun-host>[:port]&stun=<stun-host>[:port]...
// example:
// realm://public@realm.hy2.io/57f9be7c-2810-4f5b-8cb9-260bc84d6c90?stun=example.stun:3478&stun=example2.stun:3478
public record HyRealm(
    bool IsHttp,
    string Token,
    string RendezvousHost,
    int RendezvousPort,
    string RealmName,
    List<string> StunList
)
{
    public string RendezvousHostPort => RendezvousPort > 0 ? $"{RendezvousHost}:{RendezvousPort}" : RendezvousHost;

    /// <summary>
    /// sing-box realm.server_url requires a scheme (https:// or http://).
    /// </summary>
    public string ToServerUrl()
    {
        var scheme = IsHttp ? "http" : "https";
        return $"{scheme}://{RendezvousHostPort}";
    }

    public static bool TryParse(string? str, out HyRealm? realm)
    {
        realm = null;
        if (str == null)
        {
            return false;
        }
        try
        {
            var isHttp = str.StartsWith("realm+http://");
            var prefix = isHttp ? "realm+http://" : "realm://";
            if (!str.StartsWith(prefix))
            {
                return false;
            }
            var uri = new Uri(str);
            var token = Utils.UrlDecode(uri.UserInfo);
            var rendezvousHost = uri.Host;
            var rendezvousPort = uri.IsDefaultPort ? (isHttp ? 80 : 443) : uri.Port;
            var realmName = uri.AbsolutePath.TrimStart('/');
            var stunList = new List<string>();
            var query = uri.Query;
            if (!query.IsNullOrEmpty())
            {
                if (query.StartsWith('?'))
                {
                    query = query.Substring(1);
                }
                var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var idx = pair.IndexOf('=');
                    if (idx <= 0)
                    {
                        continue;
                    }
                    var key = pair.Substring(0, idx);
                    if (!key.Equals("stun", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var val = pair.Substring(idx + 1);
                    stunList.Add(Utils.UrlDecode(val));
                }
            }
            realm = new HyRealm(isHttp, token, rendezvousHost, rendezvousPort, realmName, stunList);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string ToUri()
    {
        var prefix = IsHttp ? "realm+http://" : "realm://";
        var uriBuilder = new UriBuilder
        {
            Scheme = IsHttp ? "http" : "realm",
            Host = RendezvousHost,
            Port = RendezvousPort,
            Path = "/" + RealmName,
            UserName = Utils.UrlEncode(Token),
        };
        if (StunList is { Count: > 0 })
        {
            // var query = string.Join('&', StunList.Select(s => "stun=" + Utils.UrlEncode(s)));
            // NOTE: maybe we don't need to encode the stun host:port, since it should be a valid URI component already, and encoding will make it unreadable
            var query = string.Join('&', StunList.Select(s => "stun=" + s));
            uriBuilder.Query = query;
        }
        return prefix + uriBuilder.Uri.GetComponents(
            UriComponents.UserInfo | UriComponents.HostAndPort | UriComponents.PathAndQuery, UriFormat.UriEscaped);
    }

    public string ToUriForFinalmask()
    {
        var prefix = IsHttp ? "realm+http://" : "realm://";
        var uriBuilder = new StringBuilder();
        uriBuilder.Append(prefix);
        uriBuilder.Append(Utils.UrlEncode(Token));
        uriBuilder.Append('@');
        uriBuilder.Append(RendezvousHostPort);
        uriBuilder.Append('/');
        uriBuilder.Append(Utils.UrlEncode(RealmName));
        return uriBuilder.ToString();
    }
}
