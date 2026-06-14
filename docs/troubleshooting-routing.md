# Routing troubleshooting

This page collects common routing symptoms and checks for v2rayN users.

## Requests to `169.254.169.254` are routed as `direct`

`169.254.169.254` is commonly used by cloud providers as an instance metadata service address. Some SDKs and desktop tools probe this address to detect whether they are running in a cloud environment.

On a local desktop machine, this address often has no responder. If a routing rule such as `geoip:private -> direct` matches it, the operating system may keep the TCP connection in `SYN_SENT` until it times out. This can look like a slow or hanging proxy connection in applications that wait for the probe to finish.

### Symptoms

- Core logs show a request similar to `169.254.169.254 ... [socks -> direct]`.
- On macOS or Linux, tools such as `lsof` or `netstat` show connections to `169.254.169.254:80` stuck in `SYN_SENT`.
- The issue happens in desktop development tools, IDE plugins, browsers, or SDKs that perform cloud environment detection.

### Workaround

Add a custom routing rule before broad private-network rules such as `geoip:private -> direct`:

```json
{
  "remarks": "Block cloud metadata address",
  "outboundTag": "block",
  "ip": [
    "169.254.169.254"
  ]
}
```

Blocking this address makes local cloud metadata probes fail quickly instead of waiting for a direct connection timeout.

### Notes

- Do not route this address to `proxy` unless you intentionally want the remote proxy server to access its own metadata service.
- Editing the generated core config file directly is not persistent because v2rayN regenerates it. Add the rule through v2rayN routing settings or another persistent routing configuration.
