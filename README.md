# v2rayN

<!-- ---

## 关于本 Fork

本仓库基于上游项目 [2dust/v2rayN](https://github.com/2dust/v2rayN)。
上游仍在持续更新，本 fork 会不定期同步上游变更。

本 fork 主要增加：

- 批量测试节点的 IP 地址。
- 批量测试节点的 UDP 转发功能和延迟测试，支持 DNS, STUN, Minecraft Bedrock Edition 等多种协议。
- 批量测试节点的 Nat 类型，包括绑定测试，过滤测试和映射测试。
- 可分离入站分流核心和实际出站核心，由此实现了：
  - 避免启用 TUN 时的二次路由问题。
  - 避免启用 TUN 时的回环问题。
  - 界面化支持多种出站核心（NaiveProxy、Mieru 等）。
  - 可以普通节点的方式储存和导入导出 Naive, ShadowQUIC, overtls, Mieru 等节点。
  - 测试节点时可使用自定义的出站核心。
- 可使用 cdn-cgi trace 获取 IP 地址和属地（在一定程度上属于滥用，没有提交到上游）。
- 添加~~紫薇~~延迟测试除数选项。~~(太好了，我的美西延迟终于优化到 10 ms 了！！！😭😭😭)~~
- 尝试修复一些潜在的界面 Bug。
- .NET 10 编译产物以及 r2r 产物的发布支持。
- 其他一些小改动和修复。

**注意：本 fork 并非上游替代品。**

如果你不需要上述功能，建议直接使用上游版本以获得更好的稳定性和兼容性。

**在此特别感谢上游作者 2dust 以及所有贡献者的辛勤付出！**

--- -->

A GUI client for Windows, Linux and macOS, support [Xray](https://github.com/XTLS/Xray-core)
and [sing-box](https://github.com/SagerNet/sing-box)
and [others](https://github.com/2dust/v2rayN/wiki/List-of-supported-cores)

### A GUI client for Windows, Linux and macOS. Support [Xray](https://github.com/XTLS/Xray-core) and [sing-box](https://github.com/SagerNet/sing-box) and [others](https://github.com/2dust/v2rayN/wiki/List-of-supported-cores)

[![CodeFactor](https://www.codefactor.io/repository/github/2dust/v2rayn/badge)](https://www.codefactor.io/repository/github/2dust/v2rayn)
[![Release](https://img.shields.io/github/v/release/2dust/v2rayN?logo=github&label=Release)](https://github.com/2dust/v2rayN/releases)
[![Downloads](https://img.shields.io/github/downloads/2dust/v2rayN/latest/total?logo=github&label=Downloads)](https://github.com/2dust/v2rayN/releases)
[![Telegram](https://img.shields.io/badge/Telegram-Chat-26A5E4?logo=telegram)](https://t.me/v2rayn)
 
[![Windows](https://img.shields.io/badge/Windows-supported-0078D6?logo=windows)](https://github.com/2dust/v2rayN) 
[![Linux](https://img.shields.io/badge/Linux-supported-FCC624?logo=linux&logoColor=000)](https://github.com/2dust/v2rayN) 
[![macOS](https://img.shields.io/badge/macOS-supported-000000?logo=apple)](https://github.com/2dust/v2rayN) 
[![GPG Signed](https://img.shields.io/badge/GPG-signed-4B32C3?logo=gnuprivacyguard)](https://github.com/2dust/v2rayN)


---

## Download / 下载

Download the latest release here:

在这里下载最新版本：

[https://github.com/2dust/v2rayN/releases](https://github.com/2dust/v2rayN/releases)


> [!TIP]
> v2rayN is the desktop version. For the mobile version, please visit the v2rayNG \
> v2rayN 是电脑版，手机版请访问 v2rayNG
>
> https://github.com/2dust/v2rayNG

---

## Documentation / 使用文档

Read the Wiki for usage guides and configuration details.

请阅读 Wiki 获取使用说明和配置教程。

[https://github.com/2dust/v2rayN/wiki](https://github.com/2dust/v2rayN/wiki)

---

## Supported Platforms / 支持平台

| Platform / 平台 | x64 | x86 | arm64 | riscv64 | loong64 |
| --- | --- | --- | --- | --- | --- |
| Windows | ✅ | ✅ | ✅ | - | - |
| Linux | ✅ | - | ✅ | ✅ | ✅ |
| macOS | ✅ | - | ✅ | - | - |

---

## GPG Verification / GPG 签名校验

Release files are signed with GPG to verify authenticity and integrity, helping prevent mirror, ISP, or CDN hijacking.

发布文件已使用 GPG 签名，可用于校验文件真实性与完整性，预防镜像站、运营商或 CDN 劫持。

### Fingerprint / 公钥指纹

```text
7694 5E9F 3E9A 168F 8070 F195 805D 661C
134D FAF6 8903 C199 463C 31E5 AE90 3AE0
```

---

## Community / 社区

Telegram Group / Telegram 群组：

[https://t.me/v2rayN](https://t.me/v2rayN)

Telegram Channel / Telegram 频道：

[https://t.me/github_2dust](https://t.me/github_2dust)
