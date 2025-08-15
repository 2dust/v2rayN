#!/usr/bin/env bash
set -euo pipefail

# ===== 配置 & 解析参数 =========================================================
VERSION_ARG="${1:-}"     # 传 7.13.8 或不传
WITH_CORE="both"         # 默认：同时捆绑 xray+sing-box（你之前要的默认）
AUTOSTART=0              # 1=系统级自启（/etc/xdg/autostart）

# 如果第一个参数是以 -- 开头，就不当版本号
if [[ "${VERSION_ARG:-}" == --* ]]; then
  VERSION_ARG=""
fi
# 把第一个非 --* 的参数作为版本号，用过就丢弃
if [[ -n "${VERSION_ARG:-}" ]]; then shift || true; fi

# 解析剩余可选参数
while [[ $# -gt 0 ]]; do
  case "$1" in
    --with-core)     WITH_CORE="${2:-both}"; shift 2;;
    --autostart)     AUTOSTART=1; shift;;
    --xray-ver)      XRAY_VER="${2:-}"; shift 2;;        # 指定 xray 版本（可选）
    --singbox-ver)   SING_VER="${2:-}"; shift 2;;        # 指定 sing-box 版本（可选）
    *)
      if [[ -z "${VERSION_ARG:-}" ]]; then VERSION_ARG="$1"; fi
      shift;;
  esac
done

# ===== 环境检测 ===============================================================
arch="$(uname -m)"
[[ "$arch" == "aarch64" || "$arch" == "x86_64" ]] || { echo "只支持 aarch64 / x86_64"; exit 1; }

# 依赖（打包不要用 root 执行，但这行需要 sudo）
sudo dnf -y install dotnet-sdk-8.0 rpm-build rpmdevtools curl unzip tar || sudo dnf -y install dotnet-sdk
command -v curl >/dev/null

# 根目录=脚本所在
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 子模块（容错）
if [[ -f .gitmodules ]]; then
  git submodule sync --recursive || true
  git submodule update --init --recursive || true
fi

# ===== 定位项目 ================================================================
PROJECT="v2rayN.Desktop/v2rayN.Desktop.csproj"
if [[ ! -f "$PROJECT" ]]; then
  PROJECT="$(find . -maxdepth 3 -name 'v2rayN.Desktop.csproj' | head -n1 || true)"
fi
[[ -f "$PROJECT" ]] || { echo "找不到 v2rayN.Desktop.csproj"; exit 1; }

# 版本
VERSION="${VERSION_ARG:-}"
if [[ -z "$VERSION" ]]; then
  if git describe --tags --abbrev=0 >/dev/null 2>&1; then
    VERSION="$(git describe --tags --abbrev=0)"
  else
    VERSION="0.0.0+git"
  fi
fi
VERSION="${VERSION#v}"   # 去掉前缀 v

# ===== .NET 发布（非单文件，自包含） ===========================================
dotnet clean "$PROJECT" -c Release
rm -rf "$(dirname "$PROJECT")/bin/Release/net8.0" || true

dotnet restore "$PROJECT"
dotnet publish "$PROJECT" \
  -c Release -r "$( [[ "$arch" == "aarch64" ]] && echo linux-arm64 || echo linux-x64 )" \
  -p:PublishSingleFile=false \
  -p:SelfContained=true \
  -p:IncludeNativeLibrariesForSelfExtract=true

RID_DIR="$( [[ "$arch" == "aarch64" ]] && echo linux-arm64 || echo linux-x64 )"
PUBDIR="$(dirname "$PROJECT")/bin/Release/net8.0/${RID_DIR}/publish"
[[ -d "$PUBDIR" ]]

# ===== 下载核心（可选） ========================================================
download_xray() {
  local outdir="$1" ver="${XRAY_VER:-}" url tmp zipname="xray.zip"
  mkdir -p "$outdir"
  if [[ -z "$ver" ]]; then
    # 最新版
    ver="$(curl -fsSL https://api.github.com/repos/XTLS/Xray-core/releases/latest \
        | grep -Eo '"tag_name":\s*"v[^"]+"' | sed -E 's/.*"v([^"]+)".*/\1/' | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[xray] 未获取到版本"; return 1; }

  if [[ "$arch" == "aarch64" ]]; then
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-arm64-v8a.zip"
  else
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-64.zip"
  fi
  echo "[+] 下载 xray: $url"
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
  curl -fL "$url" -o "$tmp/$zipname"
  unzip -q "$tmp/$zipname" -d "$tmp"
  install -Dm755 "$tmp/xray" "$outdir/xray"
}

download_singbox() {
  local outdir="$1" ver="${SING_VER:-}" url tmp tarname="singbox.tar.gz" bin
  mkdir -p "$outdir"
  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/SagerNet/sing-box/releases/latest \
        | grep -Eo '"tag_name":\s*"v[^"]+"' | sed -E 's/.*"v([^"]+)".*/\1/' | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[sing-box] 未获取到版本"; return 1; }

  if [[ "$arch" == "aarch64" ]]; then
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-arm64.tar.gz"
  else
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-amd64.tar.gz"
  fi
  echo "[+] 下载 sing-box: $url"
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
  curl -fL "$url" -o "$tmp/$tarname"
  tar -C "$tmp" -xzf "$tmp/$tarname"
  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box 解包后未找到可执行"; return 1; }
  install -Dm755 "$bin" "$outdir/sing-box"
}

# === Geo 规则下载（新增，仅此处为新增逻辑） ===================================
download_geo_assets() {
  local outroot="$1"
  local xray_dir="$outroot/bin/xray"
  local sbox_dir="$outroot/bin/sing_box"
  mkdir -p "$xray_dir" "$sbox_dir/rule-sets"

  echo "[+] 下载 Xray Geo（geosite/geoip/...）"
  curl -fsSL -o "$xray_dir/geosite.dat" \
    "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geosite.dat"
  curl -fsSL -o "$xray_dir/geoip.dat" \
    "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geoip.dat"
  curl -fsSL -o "$xray_dir/geoip-only-cn-private.dat" \
    "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/geoip-only-cn-private.dat"
  curl -fsSL -o "$xray_dir/Country.mmdb" \
    "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb"

  echo "[+] 下载 sing-box 规则 & DB"
  # 数据库（最新版 meta rules 可选）
  curl -fsSL -o "$sbox_dir/geoip.metadb" \
    "https://github.com/MetaCubeX/meta-rules-dat/releases/latest/download/geoip.metadb" || true

  # 官方 2dust srs rule-sets（常用子集）
  for f in \
    geoip-private.srs geoip-cn.srs geoip-facebook.srs geoip-fastly.srs \
    geoip-google.srs geoip-netflix.srs geoip-telegram.srs geoip-twitter.srs; do
    curl -fsSL -o "$sbox_dir/rule-sets/$f" \
      "https://raw.githubusercontent.com/2dust/sing-box-rules/rule-set-geoip/$f" || true
  done

  for f in \
    geosite-cn.srs geosite-gfw.srs geosite-greatfire.srs \
    geosite-geolocation-cn.srs geosite-category-ads-all.srs; do
    curl -fsSL -o "$sbox_dir/rule-sets/$f" \
      "https://raw.githubusercontent.com/2dust/sing-box-rules/rule-set-geosite/$f" || true
  done
}

# ===== 复制发布物到打包工作区 ==================================================
rpmdev-setuptree
TOPDIR="${HOME}/rpmbuild"
SPECDIR="${TOPDIR}/SPECS"
SOURCEDIR="${TOPDIR}/SOURCES"

PKGROOT="v2rayN-publish"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

mkdir -p "$WORKDIR/$PKGROOT"
cp -a "$PUBDIR/." "$WORKDIR/$PKGROOT/"

# 图标（可选）
ICON_CANDIDATE="$(dirname "$PROJECT")/../v2rayN.Desktop/v2rayN.png"
[[ -f "$ICON_CANDIDATE" ]] && cp "$ICON_CANDIDATE" "$WORKDIR/$PKGROOT/v2rayn.png" || true

# bin 目录结构（你之前的要求）
mkdir -p "$WORKDIR/$PKGROOT/bin/xray" "$WORKDIR/$PKGROOT/bin/sing_box"

# 核心
if [[ "$WITH_CORE" == "xray" || "$WITH_CORE" == "both" ]]; then
  download_xray "$WORKDIR/$PKGROOT/bin/xray" || echo "[!] xray 下载失败（略过）"
fi
if [[ "$WITH_CORE" == "sing-box" || "$WITH_CORE" == "both" ]]; then
  download_singbox "$WORKDIR/$PKGROOT/bin/sing_box" || echo "[!] sing-box 下载失败（略过）"
fi

# Geo / rule-sets（新增）
download_geo_assets "$WORKDIR/$PKGROOT" || echo "[!] Geo 规则下载失败（略过）"

tar -C "$WORKDIR" -czf "$SOURCEDIR/$PKGROOT.tar.gz" "$PKGROOT"

# ===== 生成 SPEC（单引号 heredoc + 占位符） ===================================
SPECFILE="$SPECDIR/v2rayN.spec"
cat > "$SPECFILE" <<'SPEC'
%global debug_package %{nil}
%undefine _debuginfo_subpackages
%undefine _debugsource_packages
# 避免因 .NET 运行时带出的 LTTng 旧 SONAME 造成安装失败
%global __requires_exclude ^liblttng-ust\.so\..*$

Name:           v2rayN
Version:        __VERSION__
Release:        1%{?dist}
Summary:        v2rayN (Avalonia) GUI client for Linux (x86_64/aarch64)
License:        GPL-3.0-only
URL:            https://github.com/2dust/v2rayN
ExclusiveArch:  aarch64 x86_64
Source0:        __PKGROOT__.tar.gz

# 运行期依赖（Avalonia/X11/字体/GL）
Requires:       libX11, libXrandr, libXcursor, libXi, libXext, libxcb, libXrender, libXfixes, libXinerama, libxkbcommon
Requires:       fontconfig, freetype, cairo, pango, mesa-libEGL, mesa-libGL

%description
v2rayN GUI client built with Avalonia.
Installs self-contained publish under /opt/v2rayN and a launcher 'v2rayn'.
Cores (if bundled): /opt/v2rayN/bin/xray, /opt/v2rayN/bin/sing_box.
Geo files for Xray are placed at /opt/v2rayN/bin/xray; launcher will symlink them into user's XDG data dir on first run.

%prep
%setup -q -n __PKGROOT__

%build
# no build

%install
install -dm0755 %{buildroot}/opt/v2rayN
cp -a * %{buildroot}/opt/v2rayN/

# 启动器（先 ELF，再 DLL 兜底；并为用户补齐 Geo 软链）
install -dm0755 %{buildroot}%{_bindir}
cat > %{buildroot}%{_bindir}/v2rayn << 'EOF'
#!/usr/bin/bash
set -euo pipefail
DIR="/opt/v2rayN"

# --- SYMLINK GEO into user's XDG dir (new) ---
XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
USR_GEO_DIR="$XDG_DATA_HOME/v2rayN/bin"
SYS_XRAY_DIR="$DIR/bin/xray"
mkdir -p "$USR_GEO_DIR"
for f in geosite.dat geoip.dat geoip-only-cn-private.dat Country.mmdb; do
  if [[ -f "$SYS_XRAY_DIR/$f" && ! -e "$USR_GEO_DIR/$f" ]]; then
    ln -s "$SYS_XRAY_DIR/$f" "$USR_GEO_DIR/$f" || true
  fi
done
# --- end GEO ---

# 优先原生 ELF（apphost）
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

# 图标
if [ -f "%{_builddir}/__PKGROOT__/v2rayn.png" ]; then
  install -dm0755 %{buildroot}%{_datadir}/icons/hicolor/256x256/apps
  install -m0644 %{_builddir}/__PKGROOT__/v2rayn.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/v2rayn.png
fi

%post
/usr/bin/update-desktop-database %{_datadir}/applications >/dev/null 2&> /dev/null || true
/usr/bin/gtk-update-icon-cache -f %{_datadir}/icons/hicolor >/dev/null 2&> /dev/null || true

%postun
/usr/bin/update-desktop-database %{_datadir}/applications >/dev/null 2&> /dev/null || true
/usr/bin/gtk-update-icon-cache -f %{_datadir}/icons/hicolor >/dev/null 2&> /dev/null || true

%files
%{_bindir}/v2rayn
/opt/v2rayN
%{_datadir}/applications/v2rayn.desktop
%{_datadir}/icons/hicolor/256x256/apps/v2rayn.png
SPEC

# 可选：系统级自启（追加块，原逻辑不变）
if [[ "$AUTOSTART" -eq 1 ]]; then
cat >> "$SPECFILE" <<'SPEC'
# 系统级自启入口
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

# 版本/包根名占位符注入
sed -i "s/__VERSION__/${VERSION}/g" "$SPECFILE"
sed -i "s/__PKGROOT__/${PKGROOT}/g" "$SPECFILE"

# ===== 构建 RPM ================================================================
rpmbuild -ba "$SPECFILE"

echo "Build done. RPM at:"
ls -1 "${TOPDIR}/RPMS/$( [[ "$arch" == "aarch64" ]] && echo aarch64 || echo x86_64 )/v2rayN-${VERSION}-1"*.rpm
