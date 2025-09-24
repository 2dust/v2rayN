# v2rayN 项目概述

## 项目简介

v2rayN 是一个支持 Windows、Linux 和 macOS 的 GUI 客户端，支持多种核心：
- [Xray](https://github.com/XTLS/Xray-core)
- [sing-box](https://github.com/SagerNet/sing-box)
- 以及其他多种代理核心

该项目提供了一个用户友好的界面来管理各种代理协议的配置，支持订阅管理、路由规则配置、系统代理设置等功能。

## 技术架构

### 核心技术栈
- **.NET 8.0** - 主要开发框架
- **Avalonia** - 跨平台UI框架
- **ReactiveUI** - 响应式编程框架
- **SQLite** - 本地数据存储

### 项目结构
```
v2rayN/
├── ServiceLib/           # 核心业务逻辑库
├── v2rayN/               # Windows WPF版本
├── v2rayN.Desktop/       # 跨平台Avalonia版本
├── AmazTool/             # 升级工具
└── GlobalHotKeys/        # 全局热键库
```

### 支持的协议类型
- VMess
- VLESS
- Shadowsocks
- SOCKS
- HTTP
- Trojan
- Hysteria2
- TUIC
- WireGuard
- Anytls
- 自定义配置

### 支持的核心类型
- Xray
- sing-box
- mihomo (clash)
- v2fly
- hysteria
- tuic
- naiveproxy
- 等多种核心

## 构建和运行

### Linux构建方法

#### 使用专用构建脚本（推荐）
项目提供了专门的Linux构建脚本，位于`v2rayN/build-linux.sh`：

```bash
cd v2rayN
./build-linux.sh
```

该脚本会自动完成以下步骤：
1. 还原所有NuGet包依赖
2. 构建所有项目组件
3. 发布自包含的Linux应用程序到`publish/v2rayN-linux-64/`目录

构建完成后，可执行文件位于`publish/v2rayN-linux-64/v2rayN`，可直接运行：
```bash
./publish/v2rayN-linux-64/v2rayN
```

#### 手动构建方法
也可以手动执行构建命令：

```bash
# 还原NuGet包
dotnet restore ./v2rayN.Desktop/v2rayN.Desktop.csproj
dotnet restore ./ServiceLib/ServiceLib.csproj
dotnet restore ./GlobalHotKeys/src/GlobalHotKeys/GlobalHotKeys.csproj

# 构建项目
dotnet build ./ServiceLib/ServiceLib.csproj --configuration Release
dotnet build ./GlobalHotKeys/src/GlobalHotKeys/GlobalHotKeys.csproj --configuration Release
dotnet build ./v2rayN.Desktop/v2rayN.Desktop.csproj --configuration Release

# 发布应用
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 --self-contained true -o ./publish/v2rayN-linux-64
```

### Windows构建命令
在 Ubuntu 环境下构建 Windows 版本：
```bash
cd v2rayN 
dotnet publish ./v2rayN/v2rayN.csproj -c Release -r win-x64 --self-contained=false -p:EnableWindowsTargeting=true -o $OutputPath64
dotnet publish ./v2rayN/v2rayN.csproj -c Release -r win-arm64 --self-contained=false -p:EnableWindowsTargeting=true -o $OutputPathArm64
dotnet publish ./v2rayN/v2rayN.csproj -c Release -r win-x64 --self-contained=true -p:EnableWindowsTargeting=true -o $OutputPath64Sc
dotnet publish ./AmazTool/AmazTool.csproj -c Release -r win-x64 --self-contained=false -p:EnableWindowsTargeting=true -o $OutputPath64
dotnet publish ./AmazTool/AmazTool.csproj -c Release -r win-arm64 --self-contained=false -p:EnableWindowsTargeting=true -o $OutputPathArm64
dotnet publish ./AmazTool/AmazTool.csproj -c Release -r win-x64 --self-contained=true -p:EnableWindowsTargeting=true -p:PublishTrimmed=true -o $OutputPath64Sc
```

### 打包脚本
使用以下脚本打包发布版本：
```bash
chmod 755 package-release-zip.sh
./package-release-zip.sh $OutputArch $OutputPath64
./package-release-zip.sh $OutputArchArm $OutputPathArm64
./package-release-zip.sh "windows-64-SelfContained" $OutputPath64Sc
```

## 开发约定和规范

### 代码风格
项目使用 `.editorconfig` 文件定义代码风格，主要约定包括：
- 使用 UTF-8 编码
- 缩进使用空格，大小为4个空格
- 行尾使用 CRLF
- 去除行尾空格
- 文件末尾添加新行

### C# 代码规范
- 使用 PascalCase 命名类、接口、属性和方法
- 接口名称以 'I' 开头
- 私有字段使用下划线前缀
- 使用表达式体语法（当在单行时）
- 优先使用隐式类型 var（当类型明显时）
- 使用文件作用域命名空间声明

### 架构模式
- 使用 MVVM 模式（Model-View-ViewModel）
- 使用 ReactiveUI 进行响应式编程
- 业务逻辑集中在 ServiceLib 项目中
- 使用 SQLite 进行本地数据存储