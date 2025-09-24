#!/bin/bash

# v2rayN Linux构建脚本（仅生成可执行文件）

set -e  # 遇到错误时停止执行

# 还原NuGet包
echo "正在还原NuGet包..."
dotnet restore ./v2rayN.Desktop/v2rayN.Desktop.csproj
dotnet restore ./ServiceLib/ServiceLib.csproj
dotnet restore ./GlobalHotKeys/src/GlobalHotKeys/GlobalHotKeys.csproj

# 构建项目
echo "正在构建项目..."
dotnet build ./ServiceLib/ServiceLib.csproj --configuration Release
dotnet build ./GlobalHotKeys/src/GlobalHotKeys/GlobalHotKeys.csproj --configuration Release
dotnet build ./v2rayN.Desktop/v2rayN.Desktop.csproj --configuration Release

# 发布应用
echo "正在发布应用..."
OUTPUT_DIR="./publish"
mkdir -p $OUTPUT_DIR

# 发布Desktop版本（仅生成可执行文件）
echo "发布Desktop版本..."
dotnet publish ./v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 --self-contained true -o $OUTPUT_DIR/v2rayN-linux-64

echo "构建完成！"
echo "可执行文件位于: ./publish/v2rayN-linux-64/"
echo "直接运行: ./publish/v2rayN-linux-64/v2rayN"