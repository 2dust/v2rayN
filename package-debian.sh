#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

FileName="v2rayN-${Arch}.zip"
wget -nv -O $FileName "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/$FileName"
7z x $FileName
cp -rf v2rayN-${Arch}/* $OutputPath

PackagePath="v2rayN-Package-${Arch}"
mkdir -p "${PackagePath}/DEBIAN"
mkdir -p "${PackagePath}/opt"
cp -rf $OutputPath "${PackagePath}/opt/v2rayN"
echo "When this file exists, app will not store configs under this folder" > "${PackagePath}/opt/v2rayN/NotStoreConfigHere.txt"

if [ $Arch = "linux-64" ]; then
    Arch2="amd64" 
else
    Arch2="arm64"
fi
echo $Arch2

# basic
cat >"${PackagePath}/DEBIAN/control" <<-EOF
Package: v2rayN
Version: $Version
Architecture: $Arch2
Maintainer: https://github.com/2dust/v2rayN
Depends: libc6 (>= 2.34), fontconfig (>= 2.14), xdg-utils (>= 1.1.3), libx11-6 (>= 1.7), openssl (>= 3.0), desktop-file-utils (>= 0.26)
Description: A GUI client for Windows and Linux, support Xray core and sing-box-core and others
EOF

cat >"${PackagePath}/DEBIAN/postinst" <<-EOF
if [ ! -s /usr/share/applications/v2rayN.desktop ]; then
    cat >/usr/share/applications/v2rayN.desktop<<-END
[Desktop Entry]
Name=v2rayN
Comment=A GUI client for Windows and Linux, support Xray core and sing-box-core and others
Exec=/opt/v2rayN/v2rayN
Icon=/opt/v2rayN/v2rayN.png
Terminal=false
Type=Application
Categories=Network;Application;
END
fi

update-desktop-database
EOF

sudo chmod 0755 "${PackagePath}/DEBIAN/postinst"
sudo chmod 0755 "${PackagePath}/opt/v2rayN/v2rayN"
sudo chmod 0755 "${PackagePath}/opt/v2rayN/AmazTool"

# Patch
# set owner to root:root
sudo chown -R root:root "${PackagePath}"
# set all directories to 755 (readable & traversable by all users)
sudo find "${PackagePath}/opt/v2rayN" -type d -exec chmod 755 {} +
# set all regular files to 644 (readable by all users)
sudo find "${PackagePath}/opt/v2rayN" -type f -exec chmod 644 {} +
# ensure main binaries are 755 (executable by all users)
sudo chmod 755 "${PackagePath}/opt/v2rayN/v2rayN" 2>/dev/null || true
sudo chmod 755 "${PackagePath}/opt/v2rayN/AmazTool" 2>/dev/null || true

# build deb package
sudo dpkg-deb -Zxz --build $PackagePath
sudo mv "${PackagePath}.deb" "v2rayN-${Arch}.deb"
