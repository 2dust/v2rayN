#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

FileName="KNcloud-${Arch}.zip"
wget -nv -O $FileName "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/v2rayN-${Arch}.zip"
7z x $FileName
cp -rf KNcloud-${Arch}/* $OutputPath

PackagePath="KNcloud-Package-${Arch}"
mkdir -p "$PackagePath/KNcloud.app/Contents/Resources"
cp -rf "$OutputPath" "$PackagePath/KNcloud.app/Contents/MacOS"
cp -f "$PackagePath/KNcloud.app/Contents/MacOS/KNcloud.icns" "$PackagePath/KNcloud.app/Contents/Resources/AppIcon.icns"
echo "When this file exists, app will not store configs under this folder" > "$PackagePath/KNcloud.app/Contents/MacOS/NotStoreConfigHere.txt"
chmod +x "$PackagePath/KNcloud.app/Contents/MacOS/KNcloud"

cat >"$PackagePath/KNcloud.app/Contents/Info.plist" <<-EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>English</string>
  <key>CFBundleDisplayName</key>
  <string>KNcloud</string>
  <key>CFBundleExecutable</key>
  <string>KNcloud</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>2dust.KNcloud</string>
  <key>CFBundleName</key>
  <string>KNcloud</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${Version}</string>
  <key>CSResourcesFileMapped</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSMinimumSystemVersion</key>
  <string>12.7</string>
</dict>
</plist>
EOF

create-dmg \
    --volname "KNcloud Installer" \
    --window-size 700 420 \
    --icon-size 100 \
    --icon "KNcloud.app" 160 185 \
    --hide-extension "KNcloud.app" \
    --app-drop-link 500 185 \
    "KNcloud-${Arch}.dmg" \
    "$PackagePath/KNcloud.app"
