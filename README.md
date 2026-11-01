# v2rayN

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

> ## 本 Fork 的改动 / Changes in this fork
>
> 本仓库是 [2dust/v2rayN](https://github.com/2dust/v2rayN) 的二次开发版本，基于上游 `master`（7.25.6）。
> 本次改动新增了 **ShadowsocksR (SSR) 节点支持** 与 **mihomo 核心支持**，并修复了相关的测速链路问题。
> 详细说明见下方 [Fork changes](#fork-changes--本-fork-的改动明细)。

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

Minimum OS requirements: [Release files introduction](https://github.com/2dust/v2rayN/wiki/Release-files-introduction) / 最低系统要求：[发布文件介绍](https://github.com/2dust/v2rayN/wiki/Release-files-introduction)

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

---

## Fork changes / 本 Fork 的改动明细

本 Fork 基于上游 `master`（v2rayN 7.25.6）二次开发，主要增加了以下能力。

### 1. ShadowsocksR (SSR) 节点支持

| 改动 | 说明 |
| --- | --- |
| `EConfigType.ShadowsocksR = 15` | 新增 SSR 配置类型（不与现有枚举值冲突） |
| `SsrFmt`（新增） | `ssr://` 分享链接的解析与生成，支持 base64url、IPv6、中文备注 |
| `FmtHandler` | 把 `ssr://` 接入统一的链接解析入口，可从剪贴板批量导入 |
| `ProtocolExtraItem` | 新增 `SsrProtocol` / `SsrObfs` / `SsrProtocolParam` / `SsrObfsParam`，以 JSON 存储，**不改动数据库表结构** |
| `AddServerWindow` | 新增 SSR 编辑面板（密码 / 加密方式 / 协议 / 混淆 / 协议参数 / 混淆参数） |
| `MainWindow` | 主菜单新增「添加 ShadowsocksR 服务器」 |
| `ProfileItem` / `NodeValidator` | 补齐 SSR 的合法性校验 |

### 2. mihomo 核心支持

| 改动 | 说明 |
| --- | --- |
| `Global.CoreTypes` | 由 `["Xray","sing_box"]` 扩展为 `["Xray","sing_box","mihomo"]`，核心类型下拉可直接选 mihomo |
| `CoreConfigClashProfileService`（新增） | 把任意节点生成为 mihomo(Clash.Meta) 完整配置：`mixed-port` / `external-controller` / `proxies` / `proxy-groups` / `rules` / TUN |
| `CoreConfigHandler` | 路由调整：核心为 mihomo 时走 Clash 配置生成，不再落入 Xray 分支 |
| 支持范围 | Shadowsocks、ShadowsocksR、VMess、VLESS、Trojan、SOCKS、HTTP |
| 默认核心 | 新建的 SSR 节点默认核心自动设为 mihomo（Xray 26.x 已移除 `shadowsocksr` 出站） |

### 3. 测速适配 mihomo

| 改动 | 说明 |
| --- | --- |
| `SpeedtestService.GetTestBatchItem` | 修正只挑选 Xray / sing-box 的问题——此前 mihomo 节点会被直接丢弃，永远停在「测试中」 |
| 批次分组 | 每批按 `CoreType` 分组，避免不同核心混进同一份配置 |
| 批量测速配置 | 基于 mihomo `listeners` + `proxy`，为每个节点分配独立本地端口 |
| 兜底 | 未测到的节点在结束时统一标记为「跳过」，不再卡住 |

### 4. 稳定性修复

| 问题 | 修复 |
| --- | --- |
| 本地 DNS 污染导致连接慢/失败 | 生成的 mihomo 配置开启 `sniffer`（TLS SNI / HTTP Host 还原域名），并把测速 URL 的域名自动加入 `force-domain` |
| 核心刚启动就测速导致 `-1` | `WaitForLocalPort` 在端口监听后再等待 1500ms 预热；mihomo 实测绑定端口后约 1 秒才能正常转发 |

### 5. 核心类型设置页

「设置 → 核心类型」新增 **ShadowsocksR** 与 **Anytls** 两项，可分别为其选择核心（Xray / sing_box / mihomo）。

---

## Build / 构建

```bash
dotnet build v2rayN/v2rayN/v2rayN.csproj -c Release
```

单文件发布（Windows x64）：

```bash
dotnet publish v2rayN/v2rayN/v2rayN.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

> 注意：SSR 节点只有在核心选为 mihomo 时才能运行；Xray 与 sing-box 均不支持 SSR。

