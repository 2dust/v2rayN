# Linux构建指南

本文档介绍了如何在Linux系统上构建v2rayN应用程序。

## 构建脚本

项目提供了一个专门的构建脚本 `build-linux.sh`，位于 `v2rayN/` 目录下。该脚本会执行以下操作：

1. 还原所有项目的NuGet包依赖
2. 构建所有项目（ServiceLib、GlobalHotKeys和v2rayN.Desktop）
3. 发布自包含的Linux应用程序到 `publish/v2rayN-linux-64/` 目录

## 系统要求

- Ubuntu/Debian或其他基于Debian的Linux发行版
- 已安装 .NET 8.0 SDK
- Git（用于获取源代码和子模块）

## 构建步骤

1. 确保已安装 .NET 8.0 SDK：
   ```bash
   dotnet --version
   ```

2. 进入项目目录：
   ```bash
   cd v2rayN
   ```

3. 运行构建脚本：
   ```bash
   ./build-linux.sh
   ```

4. 构建完成后，可执行文件位于：
   ```
   ./publish/v2rayN-linux-64/v2rayN
   ```

5. 直接运行应用程序：
   ```bash
   ./publish/v2rayN-linux-64/v2rayN
   ```

## 注意事项

- 构建脚本会自动处理子模块的初始化
- 构建过程可能需要几分钟时间，具体取决于系统性能
- 生成的可执行文件是自包含的，不依赖系统上的其他库
- 如果遇到权限问题，请确保构建脚本具有执行权限：
  ```bash
  chmod +x build-linux.sh
  ```