namespace v2rayN.Models
{
    public class SingboxConfig
    {
        public Log4Sbox log { get; set; }
        public Dns4Sbox? dns { get; set; }
        public List<Inbound4Sbox> inbounds { get; set; }
        public List<Outbound4Sbox> outbounds { get; set; }
        public Route4Sbox route { get; set; }
        public Experimental4Sbox? experimental { get; set; }
    }

    public class Log4Sbox
    {
        public bool? disabled { get; set; }
        public string level { get; set; }
        public string output { get; set; }
        public bool? timestamp { get; set; }
    }

    public class Dns4Sbox
    {
        public List<Server4Sbox> servers { get; set; }
        public List<Rule4Sbox> rules { get; set; }
        public string? final { get; set; }
        public string? strategy { get; set; }
        public bool? disable_cache { get; set; }
        public bool? disable_expire { get; set; }
        public bool? independent_cache { get; set; }
        public bool? reverse_mapping { get; set; }
        public string? client_subnet { get; set; }
        public Fakeip4Sbox? fakeip { get; set; }
    }

    public class Route4Sbox
    {
        public bool? auto_detect_interface { get; set; }
        public List<Rule4Sbox> rules { get; set; }
        public List<Ruleset4Sbox>? rule_set { get; set; }
    }

    [Serializable]
    public class Rule4Sbox
    {
        public string? outbound { get; set; }
        public string? server { get; set; }
        public bool? disable_cache { get; set; }
        public string? type { get; set; }
        public string? mode { get; set; }
        public bool? ip_is_private { get; set; }
        public string? client_subnet { get; set; }
        public List<string>? inbound { get; set; }
        public List<string>? protocol { get; set; }
        public List<string>? network { get; set; }
        public List<int>? port { get; set; }
        public List<string>? port_range { get; set; }
        public List<string>? geosite { get; set; }
        public List<string>? domain { get; set; }
        public List<string>? domain_suffix { get; set; }
        public List<string>? domain_keyword { get; set; }
        public List<string>? domain_regex { get; set; }
        public List<string>? geoip { get; set; }
        public List<string>? ip_cidr { get; set; }
        public List<string>? source_ip_cidr { get; set; }
        public List<string>? process_name { get; set; }
        public List<string>? rule_set { get; set; }
        public List<Rule4Sbox>? rules { get; set; }
    }

    [Serializable]
    public class Inbound4Sbox
    {
        public string type { get; set; }
        public string tag { get; set; }
        public string listen { get; set; }
        public int? listen_port { get; set; }
        public string? domain_strategy { get; set; }
        public string interface_name { get; set; }
        public string inet4_address { get; set; }
        public string? inet6_address { get; set; }
        public int? mtu { get; set; }
        public bool? auto_route { get; set; }
        public bool? strict_route { get; set; }
        public bool? endpoint_independent_nat { get; set; }
        public string? stack { get; set; }
        public bool? sniff { get; set; }
        public bool? sniff_override_destination { get; set; }
        public List<User4Sbox> users { get; set; }
    }

    public class User4Sbox
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    public class Outbound4Sbox
    {
        public string type { get; set; }
        public string tag { get; set; }
        public string? server { get; set; }
        public int? server_port { get; set; }
        public string uuid { get; set; }
        public string security { get; set; }
        public int? alter_id { get; set; }
        public string flow { get; set; }
        public int? up_mbps { get; set; }
        public int? down_mbps { get; set; }
        public string auth_str { get; set; }
        public int? recv_window_conn { get; set; }
        public int? recv_window { get; set; }
        public bool? disable_mtu_discovery { get; set; }
        public string? detour { get; set; }
        public string method { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string congestion_control { get; set; }
        public string? version { get; set; }
        public string? network { get; set; }
        public string? packet_encoding { get; set; }
        public string[]? local_address { get; set; }
        public string? private_key { get; set; }
        public string? peer_public_key { get; set; }
        public int[]? reserved { get; set; }
        public int? mtu { get; set; }
        public string? plugin { get; set; }
        public string? plugin_opts { get; set; }
        public Tls4Sbox? tls { get; set; }
        public Multiplex4Sbox? multiplex { get; set; }
        public Transport4Sbox? transport { get; set; }
        public HyObfs4Sbox? obfs { get; set; }
    }

    public class Tls4Sbox
    {
        public bool enabled { get; set; }
        public string server_name { get; set; }
        public bool? insecure { get; set; }
        public List<string> alpn { get; set; }
        public Utls4Sbox utls { get; set; }
        public Reality4Sbox reality { get; set; }
    }

    public class Multiplex4Sbox
    {
        public bool enabled { get; set; }
        public string protocol { get; set; }
        public int max_connections { get; set; }
    }

    public class Utls4Sbox
    {
        public bool enabled { get; set; }
        public string fingerprint { get; set; }
    }

    public class Reality4Sbox
    {
        public bool enabled { get; set; }
        public string public_key { get; set; }
        public string short_id { get; set; }
    }

    public class Transport4Sbox
    {
        public string? type { get; set; }
        public object? host { get; set; }
        public string? path { get; set; }
        public Headers4Sbox? headers { get; set; }

        public string? service_name { get; set; }
        public string? idle_timeout { get; set; }
        public string? ping_timeout { get; set; }
        public bool? permit_without_stream { get; set; }
    }

    public class Headers4Sbox
    {
        public string? Host { get; set; }
    }

    public class HyObfs4Sbox
    {
        public string? type { get; set; }
        public string? password { get; set; }
    }

    public class Server4Sbox
    {
        public string? tag { get; set; }
        public string? address { get; set; }
        public string? address_resolver { get; set; }
        public string? address_strategy { get; set; }
        public string? strategy { get; set; }
        public string? detour { get; set; }
        public string? client_subnet { get; set; }
    }

    public class Experimental4Sbox
    {
        public CacheFile4Sbox? cache_file { get; set; }
        public V2ray_Api4Sbox? v2ray_api { get; set; }
        public Clash_Api4Sbox? clash_api { get; set; }
    }

    public class V2ray_Api4Sbox
    {
        public string listen { get; set; }
        public Stats4Sbox stats { get; set; }
    }

    public class Clash_Api4Sbox
    {
        public string? external_controller { get; set; }
        public bool? store_selected { get; set; }
    }

    public class Stats4Sbox
    {
        public bool enabled { get; set; }
        public List<string>? inbounds { get; set; }
        public List<string>? outbounds { get; set; }
        public List<string>? users { get; set; }
    }

    public class Fakeip4Sbox
    {
        public bool enabled { get; set; }
        public string inet4_range { get; set; }
        public string inet6_range { get; set; }
    }

    public class CacheFile4Sbox
    {
        public bool enabled { get; set; }
        public string? path { get; set; }
        public string? cache_id { get; set; }
        public bool? store_fakeip { get; set; }
    }

    public class Ruleset4Sbox
    {
        public string? tag { get; set; }
        public string? type { get; set; }
        public string? format { get; set; }
        public string? path { get; set; }
        public string? url { get; set; }
        public string? download_detour { get; set; }
        public string? update_interval { get; set; }
    }
}