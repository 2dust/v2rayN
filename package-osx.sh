#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

PackagePath="v2rayN-Package-${Arch}"
mkdir -p "$PackagePath/v2rayN.app/Contents/Resources"
cp -rf "$OutputPath" "$PackagePath/v2rayN.app/Contents/MacOS"
echo "When this file exists, app will not store configs under this folder" > "$PackagePath/v2rayN.app/Contents/MacOS/NotStoreConfigHere.txt"
chmod +x "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN"

mkdir -p "$PackagePath/icons.iconset"
sips -z 16 16 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_16x16.png"
sips -z 32 32 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_16x16@2x.png"
sips -z 32 32 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_32x32.png"
sips -z 64 64 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_32x32@2x.png"
sips -z 128 128 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_128x128.png"
sips -z 256 256 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_128x128@2x.png"
sips -z 256 256 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_256x256.png"
sips -z 512 512 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_256x256@2x.png"
sips -z 512 512 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_512x512.png"
sips -z 1024 1024 "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.png" --out "$PackagePath/icons.iconset/icon_512x512@2x.png"
iconutil -c icns "$PackagePath/icons.iconset" -o "$PackagePath/v2rayN.app/Contents/Resources/AppIcon.icns"

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