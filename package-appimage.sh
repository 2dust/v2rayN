#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"

FileName="v2rayN-${Arch}.zip"
wget -nv -O $FileName "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/$FileName"
7z x $FileName -aoa
cp -rf v2rayN-${Arch}/* $OutputPath

PackagePath="v2rayN-Package-${Arch}"
mkdir -p "${PackagePath}/AppDir/opt"
cp -rf $OutputPath "${PackagePath}/AppDir/opt/v2rayN"
echo "When this file exists, app will not store configs under this folder" >"${PackagePath}/AppDir/opt/v2rayN/NotStoreConfigHere.txt"

if [ $Arch = "linux-64" ]; then
    Arch2="x86_64"
    Arch3="amd64"
else
    Arch2="aarch64"
    Arch3="arm64"
fi
echo $Arch2

# basic
cat >"${PackagePath}/AppDir/AppRun" <<-EOF
#!/bin/sh
HERE="\$(dirname "\$(readlink -f "\${0}")")"
export PATH="\${HERE}"/opt/v2rayN/:"\${PATH}"
export LD_LIBRARY_PATH="\${HERE}"/opt/v2rayN/:"\${LD_LIBRARY_PATH}"
exec "\${HERE}/opt/v2rayN/v2rayN" \$@
EOF

cat >"${PackagePath}/AppDir/v2rayN.desktop" <<-EOF
[Desktop Entry]
Name=v2rayN
Comment=A GUI client for Windows and Linux, support Xray core and sing-box-core and others
Exec=v2rayN
Icon=v2rayN
Terminal=false
Type=Application
Categories=Network;
EOF

sudo cp "${PackagePath}/AppDir/opt/v2rayN/v2rayN.png" "${PackagePath}/AppDir/v2rayN.png"
sudo dpkg --add-architecture ${Arch3}
mkdir deb_folder
cd deb_folder
apt download libicu74:${Arch3}
apt download libfontconfig1:${Arch3} || true
apt download libfontconfig:${Arch3} || true
mkdir ../output_folder
for deb in *.deb; do
    dpkg-deb -x "$deb" ../output_folder/
done
cd ..
find output_folder -type f -name "*.so*" -exec cp {} ${PackagePath}/AppDir/opt/v2rayN/ \;
find output_folder -type l -name "*.so*" -exec cp {} ${PackagePath}/AppDir/opt/v2rayN/ \;
rm -rf deb_folder output_folder

sudo chmod 0755 "${PackagePath}/AppDir/opt/v2rayN/v2rayN"
sudo chmod 0755 "${PackagePath}/AppDir/AppRun"

# desktop && PATH

wget "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
chmod a+x appimagetool-x86_64.AppImage
sudo apt install -y libfuse2
sudo ./appimagetool-x86_64.AppImage "${PackagePath}/AppDir"
sudo mv "v2rayN-${Arch2}.AppImage" "v2rayN-${Arch}.AppImage"
