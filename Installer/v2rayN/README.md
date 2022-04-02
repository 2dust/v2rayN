# Project X

[Project X](https://github.com/XTLS) originates from XTLS protocol, provides a set of network tools such as [Xray-core](https://github.com/XTLS/Xray-core) and [Xray-flutter](https://github.com/XTLS/Xray-flutter).

## License

[Mozilla Public License Version 2.0](https://github.com/XTLS/Xray-core/blob/main/LICENSE)

## Installation

- Linux Script
  - [Xray-install](https://github.com/XTLS/Xray-install)
  - [Xray-script](https://github.com/kirin10000/Xray-script)
- Docker
  - [teddysun/xray](https://hub.docker.com/r/teddysun/xray)
  - Xray-docker
- One Click
  - [ProxySU](https://github.com/proxysu/ProxySU)
  - [v2ray-agent](https://github.com/mack-a/v2ray-agent)
  - [Xray-yes](https://github.com/jiuqi9997/Xray-yes)
  - [Xray_onekey](https://github.com/wulabing/Xray_onekey)
- Magisk
  - [Xray4Magisk](https://github.com/CerteKim/Xray4Magisk)
  - [Xray_For_Magisk](https://github.com/E7KMbb/Xray_For_Magisk)
- Homebrew
  - `brew install xray`
  - [(Tap) Repository 0](https://github.com/N4FA/homebrew-xray)
  - [(Tap) Repository 1](https://github.com/xiruizhao/homebrew-xray)

## Contributing
[Code Of Conduct](https://github.com/XTLS/Xray-core/blob/main/CODE_OF_CONDUCT.md)

## Usage

[Xray-examples](https://github.com/XTLS/Xray-examples) / [VLESS-TCP-XTLS-WHATEVER](https://github.com/XTLS/Xray-examples/tree/main/VLESS-TCP-XTLS-WHATEVER)

## GUI Clients

- OpenWrt
  - [PassWall](https://github.com/xiaorouji/openwrt-passwall)
  - [Hello World](https://github.com/jerrykuku/luci-app-vssr)
  - [ShadowSocksR Plus+](https://github.com/fw876/helloworld)
  - [luci-app-xray](https://github.com/yichya/luci-app-xray) ([openwrt-xray](https://github.com/yichya/openwrt-xray))
- Windows
  - [v2rayN](https://github.com/2dust/v2rayN)
  - [Qv2ray](https://github.com/Qv2ray/Qv2ray)  (This project had been archived and currently inactive)
  - [Netch (NetFilter & TUN/TAP)](https://github.com/NetchX/Netch)
- Android
  - [v2rayNG](https://github.com/2dust/v2rayNG)
  - [AnXray](https://github.com/XTLS/AnXray)
  - [Kitsunebi](https://github.com/rurirei/Kitsunebi/tree/release_xtls)
- iOS & macOS (with M1 chip)
  - [Shadowrocket](https://apps.apple.com/app/shadowrocket/id932747118)
- macOS (Intel chip & M1 chip)
  - [Qv2ray](https://github.com/Qv2ray/Qv2ray) (This project had been archived and currently inactive)

## Credits

This repo relies on the following third-party projects:

- Special thanks:
  - [v2fly/v2ray-core](https://github.com/v2fly/v2ray-core)
- In production:
  - [gorilla/websocket](https://github.com/gorilla/websocket)
  - [lucas-clemente/quic-go](https://github.com/lucas-clemente/quic-go)
  - [pires/go-proxyproto](https://github.com/pires/go-proxyproto)
  - [seiflotfy/cuckoofilter](https://github.com/seiflotfy/cuckoofilter)
  - [google/starlark-go](https://github.com/google/starlark-go)
- For testing only:
  - [miekg/dns](https://github.com/miekg/dns)
  - [h12w/socks](https://github.com/h12w/socks)

## Compilation

### Windows

```bash
go build -o xray.exe -trimpath -ldflags "-s -w -buildid=" ./main
```

### Linux / macOS

```bash
go build -o xray -trimpath -ldflags "-s -w -buildid=" ./main
```

## Telegram

[Project X](https://t.me/projectXray)

[Project X Channel](https://t.me/projectXtls)

## Stargazers over time

[![Stargazers over time](https://starchart.cc/XTLS/Xray-core.svg)](https://starchart.cc/XTLS/Xray-core)
