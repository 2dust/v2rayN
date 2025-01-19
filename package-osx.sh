#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

FileName="v2rayN-${Arch}.zip"
wget -nv -O $FileName "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/$FileName"
7z x $FileName
cp -rf v2rayN-${Arch}/* $OutputPath

PackagePath="v2rayN-Package-${Arch}"
mkdir -p "$PackagePath/v2rayN.app/Contents/Resources"
cp -rf "$OutputPath" "$PackagePath/v2rayN.app/Contents/MacOS"
cp -f "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.icns" "$PackagePath/v2rayN.app/Contents/Resources/AppIcon.icns"
echo "When this file exists, app will not store configs under this folder" > "$PackagePath/v2rayN.app/Contents/MacOS/NotStoreConfigHere.txt"
chmod +x "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN"

cat >"$PackagePath/v2rayN.app/Contents/Info.plist" <<-EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>English</string>
  <key>CFBundleDisplayName</key>
  <string>v2rayN</string>
  <key>CFBundleExecutable</key>
  <string>v2rayN</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>2dust.v2rayN</string>
  <key>CFBundleName</key>
  <string>v2rayN</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${Version}</string>
  <key>CSResourcesFileMapped</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

create-dmg \
    --volname "v2rayN Installer" \
    --window-size 700 420 \
    --icon-size 100 \
    --icon "v2rayN.app" 160 185 \
    --hide-extension "v2rayN.app" \
    --app-drop-link 500 185 \
    "v2rayN-${Arch}.dmg" \
    "$PackagePath/v2rayN.app"

# 为生成的 DMG 文件创建 SHA256 校验值文件
SHA256File="v2rayN-${Arch}.dmg.sha256"
shasum -a 256 "v2rayN-${Arch}.dmg" > "$SHA256File"
