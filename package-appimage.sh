#!/bin/bash
set -euo pipefail

# Install deps
sudo apt update -y
sudo apt install -y libfuse2 wget file

# Get tools
wget -qO appimagetool https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
chmod +x appimagetool

# x86_64 AppDir
APPDIR_X64="AppDir-x86_64"
rm -rf "$APPDIR_X64"
mkdir -p "$APPDIR_X64/usr/lib/v2rayN" "$APPDIR_X64/usr/bin" "$APPDIR_X64/usr/share/applications" "$APPDIR_X64/usr/share/pixmaps"
cp -rf "$OutputPath64"/* "$APPDIR_X64/usr/lib/v2rayN" || true
[ -f "$APPDIR_X64/usr/lib/v2rayN/v2rayN.png" ] && cp "$APPDIR_X64/usr/lib/v2rayN/v2rayN.png" "$APPDIR_X64/usr/share/pixmaps/v2rayN.png" || true
[ -f "$APPDIR_X64/usr/lib/v2rayN/v2rayN.png" ] && cp "$APPDIR_X64/usr/lib/v2rayN/v2rayN.png" "$APPDIR_X64/v2rayN.png" || true

printf '%s\n' '#!/bin/sh' 'HERE="$(dirname "$(readlink -f "$0")")"' 'cd "$HERE/usr/lib/v2rayN"' 'exec "$HERE/usr/lib/v2rayN/v2rayN" "$@"' > "$APPDIR_X64/AppRun"
chmod +x "$APPDIR_X64/AppRun"
ln -sf usr/lib/v2rayN/v2rayN "$APPDIR_X64/usr/bin/v2rayN"
cat > "$APPDIR_X64/v2rayN.desktop" <<EOF
[Desktop Entry]
Name=v2rayN
Comment=A GUI client for Windows and Linux, support Xray core and sing-box-core and others
Exec=v2rayN
Icon=v2rayN
Terminal=false
Type=Application
Categories=Network;
EOF
install -Dm644 "$APPDIR_X64/v2rayN.desktop" "$APPDIR_X64/usr/share/applications/v2rayN.desktop"

ARCH=x86_64 ./appimagetool "$APPDIR_X64" "v2rayN-${OutputArch}.AppImage"
file "v2rayN-${OutputArch}.AppImage" | grep -q 'x86-64'

# aarch64 AppDir
APPDIR_ARM64="AppDir-aarch64"
rm -rf "$APPDIR_ARM64"
mkdir -p "$APPDIR_ARM64/usr/lib/v2rayN" "$APPDIR_ARM64/usr/bin" "$APPDIR_ARM64/usr/share/applications" "$APPDIR_ARM64/usr/share/pixmaps"
cp -rf "$OutputPathArm64"/* "$APPDIR_ARM64/usr/lib/v2rayN" || true
[ -f "$APPDIR_ARM64/usr/lib/v2rayN/v2rayN.png" ] && cp "$APPDIR_ARM64/usr/lib/v2rayN/v2rayN.png" "$APPDIR_ARM64/usr/share/pixmaps/v2rayN.png" || true
[ -f "$APPDIR_ARM64/usr/lib/v2rayN/v2rayN.png" ] && cp "$APPDIR_ARM64/usr/lib/v2rayN/v2rayN.png" "$APPDIR_ARM64/v2rayN.png" || true

printf '%s\n' '#!/bin/sh' 'HERE="$(dirname "$(readlink -f "$0")")"' 'cd "$HERE/usr/lib/v2rayN"' 'exec "$HERE/usr/lib/v2rayN/v2rayN" "$@"' > "$APPDIR_ARM64/AppRun"
chmod +x "$APPDIR_ARM64/AppRun"
ln -sf usr/lib/v2rayN/v2rayN "$APPDIR_ARM64/usr/bin/v2rayN"
cat > "$APPDIR_ARM64/v2rayN.desktop" <<EOF
[Desktop Entry]
Name=v2rayN
Comment=A GUI client for Windows and Linux, support Xray core and sing-box-core and others
Exec=v2rayN
Icon=v2rayN
Terminal=false
Type=Application
Categories=Network;
EOF
install -Dm644 "$APPDIR_ARM64/v2rayN.desktop" "$APPDIR_ARM64/usr/share/applications/v2rayN.desktop"

# aarch64 runtime
wget -qO runtime-aarch64 https://github.com/AppImage/AppImageKit/releases/download/continuous/runtime-aarch64
chmod +x runtime-aarch64

# build aarch64 AppImage
ARCH=aarch64 ./appimagetool --runtime-file ./runtime-aarch64 "$APPDIR_ARM64" "v2rayN-${OutputArchArm}.AppImage"
file "v2rayN-${OutputArchArm}.AppImage" | grep -q 'ARM aarch64'
