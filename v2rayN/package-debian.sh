#!/bin/bash

version="$1"
arch="$2"

OutputPath="v2rayN-linux-${arch}"
mkdir -p "${OutputPath}/DEBIAN"
mkdir -p "${OutputPath}/opt"
cp -r "./bin/v2rayN/linux-${arch}" "${OutputPath}/opt"
mv "${OutputPath}/opt/linux-${arch}" "${OutputPath}/opt/v2rayN"


if [ $arch = "x64" ]; then
    Arch2="amd64" 
else
    Arch2="arm64"
fi
echo $Arch2

# basic
cat >"${OutputPath}/DEBIAN/control" <<-EOF
Package: v2rayN
Version: $version
Architecture: $Arch2
Maintainer: https://github.com/2dust/v2rayN
Description: A GUI client for Windows and Linux, support Xray core and sing-box-core and others
EOF

cat >"${OutputPath}/DEBIAN/postinst" <<-EOF
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

sudo chmod 0755 "${OutputPath}/DEBIAN/postinst"
sudo chmod 0755 "${OutputPath}/opt/v2rayN/v2rayN"

# desktop && PATH

sudo dpkg-deb -Zxz --build $OutputPath