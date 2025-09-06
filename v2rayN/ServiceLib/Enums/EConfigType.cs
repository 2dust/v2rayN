namespace ServiceLib.Enums;

public enum EConfigType
{
    VMess = 1,
    Custom = 2,
    Shadowsocks = 3,
    SOCKS = 4,
    VLESS = 5,
    Trojan = 6,
    Hysteria2 = 7,
    TUIC = 8,
    WireGuard = 9,
    HTTP = 10,
    Anytls = 11,

    Group = 1000,
    PolicyGroup = 1001,
    ProxyChain = 1002,
}
