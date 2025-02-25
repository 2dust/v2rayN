#!/bin/bash

sudo apt update -y
sudo apt install -y libfuse2
wget -O pkg2appimage https://github.com/AppImageCommunity/pkg2appimage/releases/download/continuous/pkg2appimage-1eceb30-x86_64.AppImage
chmod a+x pkg2appimage
export AppImageOutputArch=$OutputArch
export OutputPath=$OutputPath64
./pkg2appimage ./pkg2appimage.yml
mv out/*.AppImage v2rayN-${AppImageOutputArch}.AppImage
export AppImageOutputArch=$OutputArchArm
export OutputPath=$OutputPathArm64
./pkg2appimage ./pkg2appimage.yml
mv out/*.AppImage v2rayN-${AppImageOutputArch}.AppImage
