# v2rayN.Bridge

A lightweight CLI that exposes [v2rayN](https://github.com/2dust/v2rayN)'s battle-tested
configuration parsing, generation, and (hopefully, in the future) other features, to external scripts, tools, and languages.

## Why?

v2rayN handles an enormous variety of proxy protocols, share links, and subscription
formats. Being wildly used by many users, either directly via the original client or indirectly via its numerous forks,
the popularity makes it sort of the "standard" for dealing with this context, be it for casual use or development. The
capability to use v2rayN's native, well-known, and widely-maintainend features directly gives you a seamless experience
for an extensive range of interests.
**v2rayN.Bridge aims to wrap the exact same `ServiceLib` code that v2rayN uses
internally and makes it available through a simple stdout interface.**

## Vision

`v2rayN.Bridge` is the first step toward **an API‑like interface for v2rayN**.
Eventually it should be able to:

- Load and manipulate the local configuration database
- Trigger speed tests, ping checks, and subscription updates
- Manage routing rules, DNS settings, and core config generation
- Serve as the backend for headless deployments, web dashboards, or
  cross‑platform GUI alternatives

Each new command is a `case` statement added to `Program.cs`.
If you can call a `ServiceLib` method from C#, you can expose it here.

## Current Features

- Parse any v2rayN share link (VMess, VLESS, Trojan, Shadowsocks, Hysteria2, TUIC, …)
- Batch‑parse subscription content
- Convert a server profile back into a share URI
- Designed for piping: JSON in, JSON out
- Self‑contained single‑file binary – no .NET runtime required on the target machine

## Build

```bash
# From the repository root (the parent directory of this README file)
dotnet publish v2rayN.Bridge -c Release -r <YOUR-OS> --self-contained true -p:PublishSingleFile=true
```

Replace `<YOUR-OS>` with `win-x64`, `linux-x64`, `osx-x64`, or `osx-arm64` accordingly.

## Usage

### CLI commands

```
v2rayN.Bridge <command> <argument>
```

| Command     | Argument                        | Output                              |
| ----------- | ------------------------------- | ----------------------------------- |
| `to-json`   | A share link (e.g. `vmess://…`) | JSON object (`ProfileItem`)         |
| `parse-sub` | Subscription text (all URLs)    | JSON array of `ProfileItem` objects |
| `to-uri`    | JSON of a `ProfileItem`         | The share link as a string          |

### Example: Python integration

```python
# create test.py next to this README file
from pathlib import Path
import subprocess
import json


class Bridge:
    def __init__(self, path_to_exec):
        self.exec = path_to_exec

    def run(self, *cmdargs) -> str:
        proc = subprocess.run([self.exec, *cmdargs], capture_output=True, text=True)
        if proc.returncode:
            raise RuntimeError(proc.stderr)
        return proc.stdout

    def load_uri(self, uri: str):
        json_str = self.run("to-json", uri)
        return json.loads(json_str)

    def dump_uri(self, config: dict):
        return self.run("to-uri", json.dumps(config))

    def extract_configs(self, sub_content):
        return json.loads(self.run("parse-sub", sub_content))


sub_content = """
this is an example of some text content, probably
a config subscription's response, potentially
including config URIs supported by v2rayN;
such as:

ss://Y2hhY2hhMjAtaWV0Zi1wb2x5MTMwNTp1MTdUM0J2cFlhYWl1VzJj@api.namasha.co:443
vmess://ew0KICAidiI6ICIyIiwNCiAgInBzIjogIiIsDQogICJhZGQiOiAiIiwNCiAgInBvcnQiOiAiMCIsDQogICJpZCI6ICIiLA0KICAiYWlkIjogIjAiLA0KICAic2N5IjogIiIsDQogICJuZXQiOiAidGNwIiwNCiAgInRscyI6ICIiLA0KICAiYWxwbiI6ICIiLA0KICAiaW5zZWN1cmUiOiAiMCINCn0=

and also pieces of plain text as well (obviously!)
"""

# individual URIs
uris = [
    "vless://7f8f9f5d-d75e-4dd4-b921-c63b56d50865@185.143.234.25:2052?encryption=none&security=none&type=ws&host=tr-st3.ge9.ir&path=%2Fpath#%C9%A2%E1%B4%87%CA%80%E1%B4%8D%E1%B4%80%C9%B4%20%C2%B9%20%7C%20%F0%9F%87%A9%F0%9F%87%AA",
    "trojan://J6aoK74aaYaReDimz0zvQw@dateandusage.a:8443?type=tcp&headerType=none",
]

config = {"log": {"loglevel": "debug"}, "dns": {"hosts": {"dns.google": ["8.8.8.8", "8.8.4.4", "2001:4860:4860::8888", "2001:4860:4860::8844"], "cloudflare-dns.com": ["104.16.249.249", "104.16.248.249", "2606:4700::6810:f8f9", "2606:4700::6810:f9f9"]}, "servers": [{"address": "119.29.29.29", "domains": ["geosite:private", "geosite:cn"], "skipFallback": True, "tag": "direct-dns-2"}, "https://cloudflare-dns.com/dns-query"], "tag": "dns-module"}, "inbounds": [{"tag": "socks", "port": 10808, "listen": "127.0.0.1", "protocol": "mixed", "sniffing": {"enabled": True, "destOverride": ["http", "tls", "fakedns+others", "fakedns", "quic"], "routeOnly": False}, "settings": {"auth": "noauth", "udp": True, "allowTransparent": False}}], "outbounds": [{"tag": "proxy", "protocol": "vless", "settings": {"vnext": [{"address": "185.143.233.200", "port": 2082, "users": [{"id": "1691eb05-2fce-4ee1-b32b-c9b4e1647992", "email": "t@t.tt", "security": "auto", "encryption": "none"} ]} ] }, "streamSettings": {"network": "ws", "wsSettings": {"path": "/", "host": "nima.hayalco.ir", "headers": {}}}, "mux": {"enabled": True, "concurrency": 8}}], "routing": {"domainStrategy": "AsIs", "rules": [{"type": "field", "inboundTag": ["api"], "outboundTag": "api"}]}, "metrics": {"tag": "api"}, "policy": {"system": {"statsOutboundUplink": True, "statsOutboundDownlink": True}}, "stats": {}}


# after build, walk through the following path and modify in
# cases of difference; based on your OS, dotnet version, etc.
exec = Path(__file__).parent.joinpath("bin", "Release", "net10.0", "win-x64", "v2rayN.Bridge.exe")
assert exec.exists()
bridge = Bridge(exec)

if __name__ == "__main__":
    extracted = bridge.extract_configs(sub_content)
    print("\nextracted configs from sub:", extracted)

    for uri in uris:
        loaded = bridge.load_uri(uri)
        print("\nloaded uri:", loaded)

    dumped = bridge.dump_uri(config)
    print("\ndumped uri:", dumped)
```

## Contributing

1. Fork v2rayN, and add your command to `v2rayN.Bridge/Program.cs`.
2. Ensure JSON output matches the `ServiceLib` model properties (PascalCase).
3. Test with your favourite scripting language.
4. Open a PR and submit.

## License

Same as v2rayN – [GPL-3.0](https://github.com/2dust/v2rayN/blob/master/LICENSE).
