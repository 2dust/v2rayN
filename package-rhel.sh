#!/usr/bin/env bash
set -euo pipefail

# ===================== 参数 =====================
V2RAYN_VER="latest"
XRAY_VER="latest"
SING_VER="latest"
ARCH_AUTO=""            # auto detect
RID=""                  # linux-x64 / linux-arm64
FD=0                    # 0=self-contained, 1=framework-dependent
AUTOSTART=0

# 兼容：位置参数传 v2rayN 版本（老用法）
if [[ "${1:-}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then V2RAYN_VER="$1"; shift || true; fi

while [[ $# -gt 0 ]]; do
  case "$1" in
    --v2rayn) V2RAYN_VER="${2:-latest}"; shift 2;;
    --xray)   XRAY_VER="${2:-latest}"; shift 2;;
    --sing|--sing-box) SING_VER="${2:-latest}"; shift 2;;
    --arch)   ARCH_AUTO="${2:-}"; shift 2;;       # x64 | arm64
    --framework-dependent|--fd) FD=1; shift;;
    --autostart) AUTOSTART=1; shift;;
    *) echo "未知参数: $1"; exit 2;;
  esac
done

# ===================== 环境准备 =====================
case "${ARCH_AUTO:-auto}" in
  x64)   RID="linux-x64"   ;;
  arm64) RID="linux-arm64" ;;
  auto|*) 
    case "$(uname -m)" in
      x86_64) RID="linux-x64" ;;
      aarch64) RID="linux-arm64" ;;
      *) echo "不支持的架构: $(uname -m)"; exit 1;;
    esac
    ;;
esac
echo "[i] RID = ${RID}"

sudo dnf -y install dotnet-sdk-8.0 rpm-build rpmdevtools curl unzip tar || sudo dnf -y install dotnet-sdk
command -v curl >/dev/null

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 子模块
if [[ -f .gitmodules ]]; then
  git submodule sync --recursive || true
  git submodule update --init --recursive || true
fi

# ===================== v2rayN 版本确认（标签） =====================
git fetch --tags -q || true
if [[ "$V2RAYN_VER" == "latest" ]]; then
  if git describe --tags --abbrev=0 >/dev/null 2>&1; then
    V2RAYN_VER="$(git describe --tags --abbrev=0)"
  else
    V2RAYN_VER="0.0.0+git"  # 没有 tag 就带 git 尾巴
  fi
fi
V2RAYN_VER="${V2RAYN_VER#v}"
echo "[i] v2rayN version = ${V2RAYN_VER}"

# ===================== .NET 发布 =====================
PROJECT="v2rayN.Desktop/v2rayN.Desktop.csproj"
if [[ ! -f "$PROJECT" ]]; then
  PROJECT="$(find . -maxdepth 3 -name 'v2rayN.Desktop.csproj' | head -n1 || true)"
fi
[[ -f "$PROJECT" ]] || { echo "找不到 v2rayN.Desktop.csproj"; exit 1; }

dotnet clean "$PROJECT" -c Release >/dev/null
PUBDIR="$(dirname "$PROJECT")/bin/Release/net8.0/${RID}/publish"
rm -rf "$PUBDIR" || true

PUBLISH_ARGS=(
  -c Release
  -r "$RID"
  -p:PublishSingleFile=false
  -p:IncludeNativeLibrariesForSelfExtract=true
)
if (( FD == 0 )); then
  PUBLISH_ARGS+=( -p:SelfContained=true )
else
  PUBLISH_ARGS+=( -p:SelfContained=false )
fi

dotnet restore "$PROJECT"
dotnet publish "$PROJECT" "${PUBLISH_ARGS[@]}"

[[ -d "$PUBDIR" ]] || { echo "发布目录不存在：$PUBDIR"; exit 1; }

# ===================== GitHub 资产解析（最新/指定版本） =====================
gh_latest_tag() { # $1=owner/repo
  curl -fsSL "https://api.github.com/repos/$1/releases/latest" \
    | grep -Eo '"tag_name":\s*"v[^"]+"' | head -n1 | sed -E 's/.*"v([^"]+)".*/\1/'
}

# 返回匹配到的 asset 下载 URL
gh_asset_url() { # $1=owner/repo  $2=version(without v or 'latest')  $3=grep-regex
  local repo="$1" ver="$2" pat="$3" tag url
  if [[ "$ver" == "latest" ]]; then
    tag="latest"
    url="$(curl -fsSL "https://api.github.com/repos/${repo}/releases/latest" \
      | grep -Eo "https://[^\"]+${pat}" | head -n1 || true)"
  else
    tag="v${ver}"
    url="$(curl -fsSL "https://api.github.com/repos/${repo}/releases/tags/${tag}" \
      | grep -Eo "https://[^\"]+${pat}" | head -n1 || true)"
  fi
  [[ -n "$url" ]] && echo "$url" || return 1
}

# xray 资产名：
#   linux-x64  -> Xray-linux-64.zip
#   linux-arm64-> Xray-linux-arm64-v8a.zip
case "$RID" in
  linux-x64)   XRAY_PAT='Xray-linux-64\.zip' ;;
  linux-arm64) XRAY_PAT='Xray-linux-arm64-v8a\.zip' ;;
esac
# sing-box 资产名：
#   linux-x64  -> sing-box-<ver>-linux-amd64.tar.gz
#   linux-arm64-> sing-box-<ver>-linux-arm64.tar.gz
case "$RID" in
  linux-x64)   SING_PAT='sing-box-[0-9.]+-linux-amd64\.tar\.gz' ;;
  linux-arm64) SING_PAT='sing-box-[0-9.]+-linux-arm64\.tar\.gz' ;;
esac

# ===================== 下载核心到 bin/{xray,sing_box} =====================
download_xray() {
  local outroot="$1" url tmp; tmp="$(mktemp -d)"
  echo "[+] 解析 xray 版本: ${XRAY_VER}"
  url="$(gh_asset_url "XTLS/Xray-core" "${XRAY_VER}" "${XRAY_PAT}")" || {
    echo "[!] 未找到 xray 资产（ver=${XRAY_VER}, rid=${RID})"; rm -rf "$tmp"; return 1; }
  echo "[+] 下载 xray: $url"
  curl -fL "$url" -o "$tmp/xray.zip"
  unzip -q "$tmp/xray.zip" -d "$tmp"
  install -Dm0755 "$tmp/xray" "$outroot/bin/xray/xray"
  rm -rf "$tmp"
}
download_sing() {
  local outroot="$1" url tmp bin; tmp="$(mktemp -d)"
  local pat="$SING_PAT"
  if [[ "$SING_VER" != "latest" ]]; then pat="${pat//[0-9.]+/${SING_VER}}"; fi
  echo "[+] 解析 sing-box 版本: ${SING_VER}"
  url="$(gh_asset_url "SagerNet/sing-box" "${SING_VER}" "${pat}")" || {
    echo "[!] 未找到 sing-box 资产（ver=${SING_VER}, rid=${RID})"; rm -rf "$tmp"; return 1; }
  echo "[+] 下载 sing-box: $url"
  curl -fL "$url" -o "$tmp/sing.tar.gz"
  tar -C "$tmp" -xzf "$tmp/sing.tar.gz"
  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box 解包后未找到二进制"; rm -rf "$tmp"; return 1; }
  install -Dm0755 "$bin" "$outroot/bin/sing_box/sing-box"
  rm -rf "$tmp"
}

WORKROOT="$(mktemp -d)"
trap 'rm -rf "$WORKROOT"' EXIT
PKGROOT="${WORKROOT}/v2rayN-publish"
mkdir -p "$PKGROOT"
cp -a "$PUBDIR/." "$PKGROOT/"

echo "[+] 捆绑核心到 /opt/v2rayN/bin/{xray,sing_box}/"
mkdir -p "$PKGROOT/bin/xray" "$PKGROOT/bin/sing_box"
download_xray "$PKGROOT" || echo "[!] xray 下载失败（继续打包）"
download_sing "$PKGROOT" || echo "[!] sing-box 下载失败（继续打包）"

# 兼容图标
ICON="$(dirname "$PROJECT")/../v2rayN.Desktop/v2rayN.png"
[[ -f "$ICON" ]] && cp "$ICON" "$PKGROOT/v2rayn.png" || true

# 打源码包
rpmdev-setuptree
TOPDIR="${HOME}/rpmbuild"
SPECDIR="${TOPDIR}/SPECS"
SOURCEDIR="${TOPDIR}/SOURCES"
tar -C "$WORKROOT" -czf "${SOURCEDIR}/v2rayN-publish.tar.gz" "v2rayN-publish"

# ===================== SPEC =====================
SPECFILE="${SPECDIR}/v2rayN.spec"
cat > "$SPECFILE" <<'SPEC'
%global debug_package %{nil}
%undefine _debuginfo_subpackages
%undefine _debugsource_packages
%global __requires_exclude ^liblttng-ust\.so\..*$

Name:           v2rayN
Version:        __V2RAYN_VER__
Release:        1%{?dist}
Summary:        v2rayN (Avalonia) GUI client
License:        GPL-3.0-only
URL:            https://github.com/2dust/v2rayN
ExclusiveArch:  aarch64 x86_64
Source0:        v2rayN-publish.tar.gz

Requires:       libX11, libXrandr, libXcursor, libXi, libXext, libxcb, libXrender, libXfixes, libXinerama, libxkbcommon
Requires:       fontconfig, freetype, cairo, pango, mesa-libEGL, mesa-libGL

%description
v2rayN GUI client built with Avalonia (.NET 8).
Installs files under /opt/v2rayN and a launcher 'v2rayn'.
Cores are placed at /opt/v2rayN/bin/xray/ and /opt/v2rayN/bin/sing_box/.
Compatibility symlinks /opt/v2rayN/xray and /opt/v2rayN/sing-box are provided.

%prep
%setup -q -n v2rayN-publish

%build
# no build

%install
install -dm0755 %{buildroot}/opt/v2rayN
cp -a * %{buildroot}/opt/v2rayN/

# 兼容软链
ln -sf bin/xray/xray         %{buildroot}/opt/v2rayN/xray
ln -sf bin/sing_box/sing-box %{buildroot}/opt/v2rayN/sing-box

# 启动器
install -dm0755 %{buildroot}%{_bindir}
cat > %{buildroot}%{_bindir}/v2rayn << 'EOF'
#!/usr/bin/bash
set -euo pipefail
DIR="/opt/v2rayN"
if [[ -x "$DIR/v2rayN" ]]; then exec "$DIR/v2rayN" "$@"; fi
for dll in v2rayN.Desktop.dll v2rayN.dll; do
  if [[ -f "$DIR/$dll" ]]; then exec /usr/bin/dotnet "$DIR/$dll" "$@"; fi
done
echo "v2rayN launcher: no executable found in $DIR" >&2
ls -l "$DIR" >&2 || true
exit 1
EOF
chmod 0755 %{buildroot}%{_bindir}/v2rayn

# 桌面文件与图标
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
if [ -f "%{_builddir}/v2rayN-publish/v2rayn.png" ]; then
  install -dm0755 %{buildroot}%{_datadir}/icons/hicolor/256x256/apps
  install -m0644 %{_builddir}/v2rayN-publish/v2rayn.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/v2rayn.png
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

%changelog
* Fri Aug 15 2025 Pack Script <builder@example.com> - __V2RAYN_VER__-1
- Auto-detect arch and fetch latest (or specific) xray/sing-box assets via GitHub API
- Place cores under /opt/v2rayN/bin/{xray,sing_box} with compatibility symlinks
- Self-contained by default; optional framework-dependent build
SPEC

sed -i "s/__V2RAYN_VER__/${V2RAYN_VER}/g" "$SPECFILE"

# ===================== 构建 RPM =====================
rpmbuild -ba "$SPECFILE"

echo "Build done. RPM at:"
ls -1 "${HOME}/rpmbuild/RPMS/"*/"v2rayN-${V2RAYN_VER}-1"*.rpm
