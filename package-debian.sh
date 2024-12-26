#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

PackagePath="v2rayN-Package-${Arch}"
mkdir -p "${PackagePath}/DEBIAN"
mkdir -p "${PackagePath}/opt"
cp -rf $OutputPath "${PackagePath}/opt/v2rayN"

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

# desktop && PATH

sudo dpkg-deb -Zxz --build $PackagePath
sudo mv "${PackagePath}.deb" "v2rayN-${Arch}.deb"