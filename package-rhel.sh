#!/usr/bin/env bash
set -euo pipefail

# ===================== 用户参数 =====================
VERSION_ARG="${1:-}"
AUTOSTART=0

# 解析可选参数
shift $(( $#>0 ? 1 : 0 )) || true
while [[ $# -gt 0 ]]; do
  case "$1" in
    --autostart) AUTOSTART=1; shift;;
    *) if [[ -z "${VERSION_ARG}" ]]; then VERSION_ARG="$1"; shift; else shift; fi;;
  esac
done

# ===================== 环境准备 =====================
[[ "$(uname -m)" == "aarch64" ]] || { echo "只支持 aarch64"; exit 1; }

# 基础依赖
sudo dnf -y install dotnet-sdk-8.0 rpm-build rpmdevtools curl unzip tar || sudo dnf -y install dotnet-sdk
command -v curl >/dev/null

# 定位仓库根
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# git 子模块（GlobalHotKeys）
if [[ -f .gitmodules ]]; then
  git submodule sync --recursive || true
  git submodule update --init --recursive || true
fi

# 定位 csproj
PROJECT="v2rayN.Desktop/v2rayN.Desktop.csproj"
if [[ ! -f "$PROJECT" ]]; then
  PROJECT="$(find . -maxdepth 3 -name 'v2rayN.Desktop.csproj' | head -n1 || true)"
fi
[[ -f "$PROJECT" ]] || { echo "找不到 v2rayN.Desktop.csproj"; exit 1; }

# 版本解析
VERSION="${VERSION_ARG}"
if [[ -z "$VERSION" ]]; then
  if git describe --tags --abbrev=0 >/dev/null 2>&1; then
    VERSION="$(git describe --tags --abbrev=0)"
  else
    VERSION="0.0.0+git"
  fi
fi
VERSION="${VERSION#v}"

# ===================== dotnet 发布（非单文件、自包含） =====================
dotnet clean "$PROJECT" -c Release
rm -rf "$(dirname "$PROJECT")/bin/Release/net8.0/linux-arm64/publish" || true
dotnet restore "$PROJECT"
dotnet publish "$PROJECT" \
  -c Release -r linux-arm64 \
  -p:PublishSingleFile=false \
  -p:SelfContained=true \
  -p:IncludeNativeLibrariesForSelfExtract=true

PUBDIR="$(dirname "$PROJECT")/bin/Release/net8.0/linux-arm64/publish"
[[ -d "$PUBDIR" ]]

# ===================== 下载并内置核心 =====================
download_xray() {
  local outdir="$1"
  mkdir -p "$outdir"
  echo "[+] 下载 Xray-core (linux-arm64) ..."
  local tmp url
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
  url="$(curl -sL https://api.github.com/repos/XTLS/Xray-core/releases/latest \
    | grep -Eo 'https://[^"]+linux-arm64\.zip' | head -n1 || true)"
  [[ -n "$url" ]] || { echo "[!] 未找到 Xray linux-arm64 资产"; return 1; }
  curl -L "$url" -o "$tmp/xray.zip"
  unzip -q "$tmp/xray.zip" -d "$tmp"
  # 主体二进制叫 xray
  install -Dm755 "$tmp/xray" "$outdir/xray"
}

download_singbox() {
  local outdir="$1"
  mkdir -p "$outdir"
  echo "[+] 下载 sing-box (linux-arm64) ..."
  local tmp url bin
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
  url="$(curl -sL https://api.github.com/repos/SagerNet/sing-box/releases/latest \
    | grep -Eo 'https://[^"]+linux-arm64\.tar\.gz' | head -n1 || true)"
  [[ -n "$url" ]] || { echo "[!] 未找到 sing-box linux-arm64 资产"; return 1; }
  curl -L "$url" -o "$tmp/singbox.tar.gz"
  tar -C "$tmp" -xzf "$tmp/singbox.tar.gz"
  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box 解包后未找到可执行"; return 1; }
  install -Dm755 "$bin" "$outdir/sing-box"
}

# ===================== 准备打包工作区 =====================
rpmdev-setuptree
TOPDIR="${HOME}/rpmbuild"
SPECDIR="${TOPDIR}/SPECS"
SOURCEDIR="${TOPDIR}/SOURCES"

PKGROOT="v2rayN-publish"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

mkdir -p "$WORKDIR/$PKGROOT"
# 拷贝发布物
cp -a "$PUBDIR/." "$WORKDIR/$PKGROOT/"

# 图标（可选）
ICON_CANDIDATE="$(dirname "$PROJECT")/../v2rayN.Desktop/v2rayN.png"
[[ -f "$ICON_CANDIDATE" ]] && cp "$ICON_CANDIDATE" "$WORKDIR/$PKGROOT/v2rayn.png" || true

# 核心：同时内置 xray & sing-box 到 /opt/v2rayN/core/
mkdir -p "$WORKDIR/$PKGROOT/core"
download_xray   "$WORKDIR/$PKGROOT/core" || echo "[!] Xray 下载失败（略过）"
download_singbox "$WORKDIR/$PKGROOT/core" || echo "[!] sing-box 下载失败（略过）"

# 打 tarball
tar -C "$WORKDIR" -czf "$SOURCEDIR/$PKGROOT.tar.gz" "$PKGROOT"

# ===================== 生成 SPEC（单引号 heredoc） =====================
SPECFILE="$SPECDIR/v2rayN.spec"
cat > "$SPECFILE" <<'SPEC'
%global debug_package %{nil}
%undefine _debuginfo_subpackages
%undefine _debugsource_packages
# 排除 .NET 扫描到的 lttng 旧 SONAME 自动依赖
%global __requires_exclude ^liblttng-ust\.so\..*$

Name:           v2rayN
Version:        __VERSION__
Release:        1%{?dist}
Summary:        v2rayN (Avalonia) GUI client for Linux ARM64
License:        GPL-3.0-only
URL:            https://github.com/2dust/v2rayN
ExclusiveArch:  aarch64
Source0:        __PKGROOT__.tar.gz

# 运行期依赖（Avalonia/X11/字体/GL）
Requires:       libX11, libXrandr, libXcursor, libXi, libXext, libxcb, libXrender, libXfixes, libXinerama, libxkbcommon
Requires:       fontconfig, freetype, cairo, pango, mesa-libEGL, mesa-libGL

%description
v2rayN GUI client built with Avalonia for Linux ARM64.
Installs self-contained publish under /opt/v2rayN and a launcher 'v2rayn'.
Bundled cores: xray and sing-box at /opt/v2rayN/core/.

%prep
%setup -q -n __PKGROOT__

%build
# no build

%install
install -dm0755 %{buildroot}/opt/v2rayN
cp -a * %{buildroot}/opt/v2rayN/

# 启动器（先 ELF，再 DLL 兜底）
install -dm0755 %{buildroot}%{_bindir}
cat > %{buildroot}%{_bindir}/v2rayn << 'EOF'
#!/usr/bin/bash
set -euo pipefail
DIR="/opt/v2rayN"
# 优先原生 ELF
if [[ -x "$DIR/v2rayN" ]]; then exec "$DIR/v2rayN" "$@"; fi
# DLL 兜底（framework-dependent 发布时）
for dll in v2rayN.Desktop.dll v2rayN.dll; do
  if [[ -f "$DIR/$dll" ]]; then exec /usr/bin/dotnet "$DIR/$dll" "$@"; fi
done
echo "v2rayN launcher: no executable found in $DIR" >&2
ls -l "$DIR" >&2 || true
exit 1
EOF
chmod 0755 %{buildroot}%{_bindir}/v2rayn

# 桌面文件
install -dm0755 %{buildroot}%{_datadir}/applications
cat > %{buildroot}%{_datadir}/applications/v2rayn.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=v2rayN
Comment=GUI client for Xray / sing-box
Exec=v2rayn
Icon=v2rayn
Terminal=false
Categories=Network;
EOF

# 图标（从构建目录复制）
if [ -f "%{_builddir}/__PKGROOT__/v2rayn.png" ]; then
  install -dm0755 %{buildroot}%{_datadir}/icons/hicolor/256x256/apps
  install -m0644 %{_builddir}/__PKGROOT__/v2rayn.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/v2rayn.png
fi

%post
/usr/bin/update-desktop-database %{_datadir}/applications >/dev/null 2>&1 || true
/usr/bin/gtk-update-icon-cache -f %{_datadir}/icons/hicolor >/dev/null 2>&1 || true

%postun
/usr/bin/update-desktop-database %{_datadir}/applications >/dev/null 2>&1 || true
/usr/bin/gtk-update-icon-cache -f %{_datadir}/icons/hicolor >/dev/null 2>&1 || true

%files
%{_bindir}/v2rayn
/opt/v2rayN
%{_datadir}/applications/v2rayn.desktop
%{_datadir}/icons/hicolor/256x256/apps/v2rayn.png
SPEC

# 可选：系统级自启
if [[ "$AUTOSTART" -eq 1 ]]; then
cat >> "$SPECFILE" <<'SPEC'
%install
install -dm0755 %{buildroot}/etc/xdg/autostart
cat > %{buildroot}/etc/xdg/autostart/v2rayn.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=v2rayN (Autostart)
Exec=v2rayn
X-GNOME-Autostart-enabled=true
NoDisplay=false
EOF

%files
%config(noreplace) /etc/xdg/autostart/v2rayn.desktop
SPEC
fi

# changelog 与占位符替换
cat >> "$SPECFILE" <<'SPEC'

%changelog
* Fri Aug 15 2025 Pack Script <builder@example.com> - __VERSION__-1
- SelfContained non-single-file build
- Bundle xray & sing-box under /opt/v2rayN/core/
- Robust launcher (ELF first, DLL fallback)
- Exclude lttng .so auto-req to improve RHEL compatibility
SPEC

sed -i "s/__VERSION__/${VERSION}/g" "$SPECFILE"
sed -i "s/__PKGROOT__/${PKGROOT}/g" "$SPECFILE"

# ===================== 构建 RPM =====================
rpmbuild -ba "$SPECFILE"

echo "Build done. RPM at:"
ls -1 "${TOPDIR}/RPMS/aarch64/v2rayN-${VERSION}-1"*.aarch64.rpm
