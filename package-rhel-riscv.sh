#!/usr/bin/env bash
set -euo pipefail

VERSION_ARG=""
WITH_CORE="both"
FORCE_NETCORE=0
BUILD_FROM=""
XRAY_VER="${XRAY_VER:-}"
SING_VER="${SING_VER:-}"

MIN_KERNEL="5.10"
PKGROOT="v2rayN-publish"
PROJECT_HINT="v2rayN.Desktop/v2rayN.Desktop.csproj"
RPM_TOPDIR="${HOME}/rpmbuild"
DOTNET_TFM="net10.0"
DOTNET_RISCV_VERSION="10.0.107"
DOTNET_RISCV_BASE="https://github.com/xujiegb/dotnet-riscv/releases/download"
DOTNET_RISCV_FILE="dotnet-sdk-${DOTNET_RISCV_VERSION}-linux-riscv64.tar.gz"
DOTNET_SDK_URL="${DOTNET_RISCV_BASE}/${DOTNET_RISCV_VERSION}/${DOTNET_RISCV_FILE}"
SKIA_VER="${SKIA_VER:-3.119.2}"
HARFBUZZ_VER="${HARFBUZZ_VER:-8.3.1.3}"

OS_ID=""
OS_NAME=""
OS_VERSION_ID=""
HOST_ARCH=""
SCRIPT_DIR=""
PROJECT=""
VERSION=""
BUILT_ALL=0

declare -a BUILT_RPMS=()

die() {
  echo "$*" >&2
  exit 1
}

parse_args() {
  local first_arg="${1:-}"

  if [[ -n "$first_arg" && "$first_arg" != --* ]]; then
    VERSION_ARG="$first_arg"
    shift || true
  fi

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --with-core)   WITH_CORE="${2:-both}"; shift 2 ;;
      --xray-ver)    XRAY_VER="${2:-}"; shift 2 ;;
      --singbox-ver) SING_VER="${2:-}"; shift 2 ;;
      --netcore)     FORCE_NETCORE=1; shift ;;
      --buildfrom)   BUILD_FROM="${2:-}"; shift 2 ;;
      *)
        [[ -n "${VERSION_ARG:-}" ]] || VERSION_ARG="$1"
        shift
        ;;
    esac
  done

  if [[ -n "${VERSION_ARG:-}" && -n "${BUILD_FROM:-}" ]]; then
    die "You cannot specify both an explicit version and --buildfrom at the same time.
        Provide either a version (e.g. 7.14.0) OR --buildfrom 1|2|3."
  fi
}

detect_environment() {
  local current_kernel=""
  local lowest=""

  . /etc/os-release

  OS_ID="${ID:-}"
  OS_NAME="${NAME:-$OS_ID}"
  OS_VERSION_ID="${VERSION_ID:-}"
  HOST_ARCH="$(uname -m)"

  case "$OS_ID" in
    rhel|rocky|almalinux|fedora|centos)
      echo "Detected supported system: ${OS_NAME:-$OS_ID} ${OS_VERSION_ID:-}"
      ;;
    *)
      die "Unsupported system: ${OS_NAME:-unknown} (${OS_ID:-unknown}).
This script only supports: RHEL / Rocky / AlmaLinux / Fedora / CentOS."
      ;;
  esac

  case "$HOST_ARCH" in
    riscv64) ;;
    *) die "Only supports riscv64" ;;
  esac

  current_kernel="$(uname -r)"
  lowest="$(printf '%s\n%s\n' "$MIN_KERNEL" "$current_kernel" | sort -V | head -n1)"

  [[ "$lowest" == "$MIN_KERNEL" ]] || die "Kernel $current_kernel is below $MIN_KERNEL"
  echo "[OK] Kernel $current_kernel verified."
}

install_dependencies() {
  local install_ok=0
  local tmp_dotnet=""

  if command -v dnf >/dev/null 2>&1; then
    sudo dnf -y install \
      rpm-build rpmdevtools curl unzip tar jq rsync git python3 \
      glibc-devel kernel-headers libatomic file ca-certificates libicu \
      && install_ok=1

    mkdir -p "$HOME/.dotnet"
    tmp_dotnet="$(mktemp -d)"
    curl -fL "$DOTNET_SDK_URL" -o "$tmp_dotnet/$DOTNET_RISCV_FILE"
    tar -C "$HOME/.dotnet" -xzf "$tmp_dotnet/$DOTNET_RISCV_FILE"
    rm -rf "$tmp_dotnet"

    export PATH="$HOME/.dotnet:$PATH"
    export DOTNET_ROOT="$HOME/.dotnet"

    dotnet --info >/dev/null 2>&1 || install_ok=0
  fi

  if [[ "$install_ok" -ne 1 ]]; then
    echo "Could not auto-install dependencies for '$OS_ID'. Make sure these are available:"
    echo "dotnet-riscv SDK, curl, unzip, tar, rsync, git, python3, rpm, rpmdevtools, rpm-build (on Red Hat branch)"
    exit 1
  fi
}

prepare_workspace() {
  SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
  cd "$SCRIPT_DIR"

  if [[ -f .gitmodules ]]; then
    git submodule sync --recursive || true
    git submodule update --init --recursive || true
  fi

  PROJECT="$PROJECT_HINT"
  [[ -f "$PROJECT" ]] || PROJECT="$(find . -maxdepth 3 -name 'v2rayN.Desktop.csproj' | head -n1 || true)"
  [[ -f "$PROJECT" ]] || die "v2rayN.Desktop.csproj not found"
}

choose_channel() {
  local ch="latest"
  local sel=""

  if [[ -n "${BUILD_FROM:-}" ]]; then
    case "$BUILD_FROM" in
      1) echo "latest"; return 0 ;;
      2) echo "prerelease"; return 0 ;;
      3) echo "keep"; return 0 ;;
      *) die "[ERROR] Invalid --buildfrom value: ${BUILD_FROM}. Use 1|2|3." ;;
    esac
  fi

  if [[ -t 0 ]]; then
    echo "[?] Choose v2rayN release channel:" >&2
    echo "    1) Latest (stable)  [default]" >&2
    echo "    2) Pre-release (preview)" >&2
    echo "    3) Keep current (do nothing)" >&2
    printf "Enter 1, 2 or 3 [default 1]: " >&2

    if read -r sel </dev/tty; then
      case "${sel:-}" in
        2) ch="prerelease" ;;
        3) ch="keep" ;;
      esac
    fi
  fi

  echo "$ch"
}

get_latest_tag_latest() {
  curl -fsSL "https://api.github.com/repos/2dust/v2rayN/releases/latest" \
    | jq -re '.tag_name' \
    | sed 's/^v//'
}

get_latest_tag_prerelease() {
  curl -fsSL "https://api.github.com/repos/2dust/v2rayN/releases?per_page=20" \
    | jq -re 'first(.[] | select(.prerelease == true) | .tag_name)' \
    | sed 's/^v//'
}

sync_submodules() {
  if [[ -f .gitmodules ]]; then
    git submodule sync --recursive || true
    git submodule update --init --recursive || true
  fi
}

git_try_checkout() {
  local want="$1"
  local ref=""

  if git rev-parse --git-dir >/dev/null 2>&1; then
    git fetch --tags --force --prune --depth=1 || true
    git rev-parse "refs/tags/${want}" >/dev/null 2>&1 && ref="$want"

    if [[ -n "$ref" ]]; then
      echo "[OK] Found ref '${ref}', checking out..."
      git checkout -f "$ref"
      sync_submodules
      return 0
    fi
  fi

  return 1
}

apply_channel_or_keep() {
  local ch="$1"
  local tag=""

  if [[ "$ch" == "keep" ]]; then
    echo "[*] Keep current repository state (no checkout)."
    VERSION="$(git describe --tags --abbrev=0 2>/dev/null || echo '0.0.0+git')"
    VERSION="${VERSION#v}"
    return 0
  fi

  echo "[*] Resolving ${ch} tag from GitHub releases..."

  case "$ch" in
    latest)     tag="$(get_latest_tag_latest || true)" ;;
    prerelease) tag="$(get_latest_tag_prerelease || true)" ;;
    *)          die "Failed to resolve latest tag for channel '${ch}'." ;;
  esac

  [[ -n "$tag" ]] || die "Failed to resolve latest tag for channel '${ch}'."

  echo "[*] Latest tag for '${ch}': ${tag}"
  git_try_checkout "$tag" || die "Failed to checkout '${tag}'."
  VERSION="${tag#v}"
}

resolve_version() {
  if git rev-parse --git-dir >/dev/null 2>&1; then
    if [[ -n "${VERSION_ARG:-}" ]]; then
      local clean_ver="${VERSION_ARG#v}"

      if git_try_checkout "$clean_ver"; then
        VERSION="$clean_ver"
      else
        echo "[WARN] Tag '${VERSION_ARG}' not found."
        apply_channel_or_keep "$(choose_channel)"
      fi
    else
      apply_channel_or_keep "$(choose_channel)"
    fi
  else
    echo "Current directory is not a git repo; proceeding on current tree."
    VERSION="${VERSION_ARG:-0.0.0}"
  fi

  VERSION="${VERSION#v}"
  echo "[*] GUI version resolved as: ${VERSION}"
}

apply_riscv_patch() {
  local f=""

  find . -type f \( -name "*.csproj" -o -name "*.props" -o -name "*.targets" \) \
    -exec sed -Ei 's#<TargetFramework>[^<]+</TargetFramework>#<TargetFramework>'"$DOTNET_TFM"'</TargetFramework>#g' {} +

  while IFS= read -r -d '' f; do
    sed -i \
      -e "s#<PackageVersion Include=\"SkiaSharp\" Version=\"[^\"]*\" */>#<PackageVersion Include=\"SkiaSharp\" Version=\"$SKIA_VER\" />#g" \
      -e "s#<PackageVersion Include=\"SkiaSharp.NativeAssets.Linux\" Version=\"[^\"]*\" */>#<PackageVersion Include=\"SkiaSharp.NativeAssets.Linux\" Version=\"$SKIA_VER\" />#g" \
      -e "s#<PackageVersion Include=\"HarfBuzzSharp\" Version=\"[^\"]*\" */>#<PackageVersion Include=\"HarfBuzzSharp\" Version=\"$HARFBUZZ_VER\" />#g" \
      -e "s#<PackageVersion Include=\"HarfBuzzSharp.NativeAssets.Linux\" Version=\"[^\"]*\" */>#<PackageVersion Include=\"HarfBuzzSharp.NativeAssets.Linux\" Version=\"$HARFBUZZ_VER\" />#g" \
      "$f"

    grep -q 'PackageVersion Include="SkiaSharp"' "$f" || \
      sed -i "/<\/ItemGroup>/i\    <PackageVersion Include=\"SkiaSharp\" Version=\"$SKIA_VER\" />" "$f"

    grep -q 'PackageVersion Include="SkiaSharp.NativeAssets.Linux"' "$f" || \
      sed -i "/<\/ItemGroup>/i\    <PackageVersion Include=\"SkiaSharp.NativeAssets.Linux\" Version=\"$SKIA_VER\" />" "$f"

    grep -q 'PackageVersion Include="HarfBuzzSharp"' "$f" || \
      sed -i "/<\/ItemGroup>/i\    <PackageVersion Include=\"HarfBuzzSharp\" Version=\"$HARFBUZZ_VER\" />" "$f"

    grep -q 'PackageVersion Include="HarfBuzzSharp.NativeAssets.Linux"' "$f" || \
      sed -i "/<\/ItemGroup>/i\    <PackageVersion Include=\"HarfBuzzSharp.NativeAssets.Linux\" Version=\"$HARFBUZZ_VER\" />" "$f"
  done < <(find . -type f -name 'Directory.Packages.props' -print0)

  f="$(find "$DOTNET_ROOT/sdk/$(dotnet --version)" -type f -name 'Microsoft.NETCoreSdk.BundledVersions.props' | head -n1 || true)"
  if [[ -f "$f" ]] && ! grep -q 'linux-riscv64' "$f"; then
    sed -i \
      -e 's/linux-arm64/&;linux-riscv64/g' \
      -e 's/linux-musl-arm64/&;linux-musl-riscv64/g' \
      "$f"
  fi
}

copy_skiasharp_native_riscv64() {
  local outdir="$1"
  local skia_so=""
  local harfbuzz_so=""

  mkdir -p "$outdir"

  skia_so="$(find "$HOME/.nuget/packages" -path "*/skiasharp.nativeassets.linux/${SKIA_VER}/runtimes/linux-riscv64/native/libSkiaSharp.so" | head -n1 || true)"
  [[ -n "$skia_so" ]] || skia_so="$(find "$HOME/.nuget/packages" -path "*/runtimes/linux-riscv64/native/libSkiaSharp.so" | head -n1 || true)"

  harfbuzz_so="$(find "$HOME/.nuget/packages" -path "*/harfbuzzsharp.nativeassets.linux/${HARFBUZZ_VER}/runtimes/linux-riscv64/native/libHarfBuzzSharp.so" | head -n1 || true)"
  [[ -n "$harfbuzz_so" ]] || harfbuzz_so="$(find "$HOME/.nuget/packages" -path "*/runtimes/linux-riscv64/native/libHarfBuzzSharp.so" | head -n1 || true)"

  if [[ -n "$skia_so" && -f "$skia_so" ]]; then
    echo "[+] Copy libSkiaSharp.so from NuGet cache"
    install -m 755 "$skia_so" "$outdir/libSkiaSharp.so"
  else
    echo "[WARN] libSkiaSharp.so for linux-riscv64 not found in NuGet cache"
  fi

  if [[ -n "$harfbuzz_so" && -f "$harfbuzz_so" ]]; then
    echo "[+] Copy libHarfBuzzSharp.so from NuGet cache"
    install -m 755 "$harfbuzz_so" "$outdir/libHarfBuzzSharp.so"
  else
    echo "[WARN] libHarfBuzzSharp.so for linux-riscv64 not found in NuGet cache"
  fi
}

xray_url_for_rid() {
  local rid="$1"
  local ver="$2"

  case "$rid" in
    linux-riscv64) echo "https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-riscv64.zip" ;;
    *)             return 1 ;;
  esac
}

singbox_url_for_rid() {
  local rid="$1"
  local ver="$2"

  case "$rid" in
    linux-riscv64) echo "https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-riscv64.tar.gz" ;;
    *)             return 1 ;;
  esac
}

bundle_url_for_rid() {
  local rid="$1"

  case "$rid" in
    linux-riscv64) echo "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-riscv64.zip" ;;
    *)             return 1 ;;
  esac
}

download_xray() {
  local outdir="$1"
  local rid="$2"
  local ver="${XRAY_VER:-}"
  local url=""
  local tmp=""

  mkdir -p "$outdir"

  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/XTLS/Xray-core/releases/latest \
      | grep -Eo '"tag_name":\s*"v[^"]+"' \
      | sed -E 's/.*"v([^"]+)".*/\1/' \
      | head -n1)" || true
  fi

  [[ -n "$ver" ]] || { echo "[xray] Failed to get version"; return 1; }
  url="$(xray_url_for_rid "$rid" "$ver")" || { echo "[xray] Unsupported RID: $rid"; return 1; }

  echo "[+] Download xray: $url"

  tmp="$(mktemp -d)"
  curl -fL "$url" -o "$tmp/xray.zip" || { rm -rf "$tmp"; return 1; }
  unzip -q "$tmp/xray.zip" -d "$tmp" || { rm -rf "$tmp"; return 1; }
  install -m 755 "$tmp/xray" "$outdir/xray" || { rm -rf "$tmp"; return 1; }
  rm -rf "$tmp"
}

download_singbox() {
  local outdir="$1"
  local rid="$2"
  local ver="${SING_VER:-}"
  local url=""
  local tmp=""
  local bin=""
  local cronet=""

  mkdir -p "$outdir"

  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/SagerNet/sing-box/releases/latest \
      | grep -Eo '"tag_name":\s*"v[^"]+"' \
      | sed -E 's/.*"v([^"]+)".*/\1/' \
      | head -n1)" || true
  fi

  [[ -n "$ver" ]] || { echo "[sing-box] Failed to get version"; return 1; }
  url="$(singbox_url_for_rid "$rid" "$ver")" || { echo "[sing-box] Unsupported RID: $rid"; return 1; }

  echo "[+] Download sing-box: $url"

  tmp="$(mktemp -d)"
  curl -fL "$url" -o "$tmp/singbox.tar.gz" || { rm -rf "$tmp"; return 1; }
  tar -C "$tmp" -xzf "$tmp/singbox.tar.gz" || { rm -rf "$tmp"; return 1; }

  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box unpack failed"; rm -rf "$tmp"; return 1; }

  install -m 755 "$bin" "$outdir/sing-box" || { rm -rf "$tmp"; return 1; }

  cronet="$(find "$tmp" -type f -name 'libcronet*.so*' | head -n1 || true)"
  [[ -n "$cronet" ]] && install -m 644 "$cronet" "$outdir/libcronet.so" || true

  rm -rf "$tmp"
}

unify_geo_layout() {
  local outroot="$1"
  local n
  local names=(
    geosite.dat
    geoip.dat
    geoip-only-cn-private.dat
    Country.mmdb
    geoip.metadb
  )

  mkdir -p "$outroot/bin"

  for n in "${names[@]}"; do
    if [[ -f "$outroot/bin/xray/$n" ]]; then
      mv -f "$outroot/bin/xray/$n" "$outroot/bin/$n"
    fi
  done
}

download_geo_assets() {
  local outroot="$1"
  local bin_dir="$outroot/bin"
  local srss_dir="$bin_dir/srss"
  local f=""

  mkdir -p "$bin_dir" "$srss_dir"

  echo "[+] Download Xray Geo to ${bin_dir}"
  curl -fsSL -o "$bin_dir/geosite.dat" "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geosite.dat"
  curl -fsSL -o "$bin_dir/geoip.dat" "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geoip.dat"
  curl -fsSL -o "$bin_dir/geoip-only-cn-private.dat" "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/geoip-only-cn-private.dat"
  curl -fsSL -o "$bin_dir/Country.mmdb" "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb"

  echo "[+] Download sing-box rule DB & rule-sets"
  curl -fsSL -o "$bin_dir/geoip.metadb" "https://github.com/MetaCubeX/meta-rules-dat/releases/latest/download/geoip.metadb"

  for f in geoip-private.srs geoip-cn.srs geoip-facebook.srs geoip-fastly.srs geoip-google.srs geoip-netflix.srs geoip-telegram.srs geoip-twitter.srs; do
    curl -fsSL -o "$srss_dir/$f" "https://raw.githubusercontent.com/2dust/sing-box-rules/refs/heads/rule-set-geoip/$f"
  done

  for f in geosite-cn.srs geosite-gfw.srs geosite-google.srs geosite-greatfire.srs geosite-geolocation-cn.srs geosite-category-ads-all.srs geosite-private.srs; do
    curl -fsSL -o "$srss_dir/$f" "https://raw.githubusercontent.com/2dust/sing-box-rules/refs/heads/rule-set-geosite/$f"
  done

  unify_geo_layout "$outroot"
}

populate_assets_zip_mode() {
  local outroot="$1"
  local rid="$2"
  local url=""
  local tmp=""
  local nested_dir=""

  url="$(bundle_url_for_rid "$rid")" || { echo "[!] Bundle unsupported RID: $rid"; return 1; }

  echo "[+] Try v2rayN bundle archive: $url"

  tmp="$(mktemp -d)"
  curl -fL "$url" -o "$tmp/v2rayn.zip" || { echo "[!] Bundle download failed"; rm -rf "$tmp"; return 1; }
  unzip -q "$tmp/v2rayn.zip" -d "$tmp" || { echo "[!] Bundle unzip failed"; rm -rf "$tmp"; return 1; }

  if [[ -d "$tmp/bin" ]]; then
    mkdir -p "$outroot/bin"
    rsync -a "$tmp/bin/" "$outroot/bin/"
  else
    rsync -a "$tmp/" "$outroot/"
  fi

  rm -f "$outroot/v2rayn.zip" 2>/dev/null || true
  find "$outroot" -type d -name "mihomo" -prune -exec rm -rf {} + 2>/dev/null || true

  nested_dir="$(find "$outroot" -maxdepth 1 -type d -name 'v2rayN-linux-*' | head -n1 || true)"
  if [[ -n "$nested_dir" && -d "$nested_dir/bin" ]]; then
    mkdir -p "$outroot/bin"
    rsync -a "$nested_dir/bin/" "$outroot/bin/"
    rm -rf "$nested_dir"
  fi

  unify_geo_layout "$outroot"
  rm -rf "$tmp"

  echo "[+] Bundle extracted to $outroot"
}

populate_assets_netcore_mode() {
  local outroot="$1"
  local rid="$2"

  mkdir -p "$outroot/bin/xray" "$outroot/bin/sing_box"

  if [[ "$WITH_CORE" == "xray" || "$WITH_CORE" == "both" ]]; then
    download_xray "$outroot/bin/xray" "$rid" || echo "[!] xray download failed (skipped)"
  fi

  if [[ "$WITH_CORE" == "sing-box" || "$WITH_CORE" == "both" ]]; then
    download_singbox "$outroot/bin/sing_box" "$rid" || echo "[!] sing-box download failed (skipped)"
  fi

  download_geo_assets "$outroot" || echo "[!] Geo rules download failed (skipped)"
}

stage_runtime_assets() {
  local outroot="$1"
  local rid="$2"

  mkdir -p "$outroot/bin/xray" "$outroot/bin/sing_box"

  if [[ "$FORCE_NETCORE" -eq 0 ]]; then
    if populate_assets_zip_mode "$outroot" "$rid"; then
      echo "[*] Using v2rayN bundle archive."
    else
      echo "[*] Bundle failed, fallback to separate core + rules."
      populate_assets_netcore_mode "$outroot" "$rid"
    fi
  else
    echo "[*] --netcore specified: use separate core + rules."
    populate_assets_netcore_mode "$outroot" "$rid"
  fi
}

describe_target() {
  local short="$1"

  case "$short" in
    riscv64) printf '%s\n%s\n%s\n' "linux-riscv64" "riscv64" "riscv64" ;;
    *)       echo "Unknown arch '$short' (use riscv64)" >&2; return 1 ;;
  esac
}

publish_binary() {
  local rid="$1"

  dotnet clean "$PROJECT" -c Release -p:TargetFramework="$DOTNET_TFM"
  rm -rf "$(dirname "$PROJECT")/bin/Release/${DOTNET_TFM}" || true
  dotnet restore "$PROJECT" -r "$rid" -p:TargetFramework="$DOTNET_TFM"
  dotnet publish "$PROJECT" -c Release -r "$rid" -p:TargetFramework="$DOTNET_TFM" -p:PublishSingleFile=false -p:SelfContained=true
}

write_spec_file() {
  local specfile="$1"

  cat > "$specfile" <<'SPEC'
%global debug_package %{nil}
%undefine _debuginfo_subpackages
%undefine _debugsource_packages
%global __requires_exclude ^liblttng-ust\.so\..*$

Name:           v2rayN
Version:        __VERSION__
Release:        1%{?dist}
Summary:        v2rayN (Avalonia) GUI client for Linux (riscv64)
License:        GPL-3.0-only
URL:            https://github.com/2dust/v2rayN
BugURL:         https://github.com/2dust/v2rayN/issues
ExclusiveArch:  riscv64
Source0:        __PKGROOT__.tar.gz

Requires:       cairo, pango, openssl, mesa-libEGL, mesa-libGL
Requires:       glibc >= 2.34
Requires:       fontconfig >= 2.13.1
Requires:       desktop-file-utils >= 0.26
Requires:       xdg-utils >= 1.1.3
Requires:       coreutils >= 8.32
Requires:       bash >= 5.1
Requires:       freetype >= 2.10

%description
v2rayN Linux for Red Hat Enterprise Linux
Support vless / vmess / Trojan / http / socks / Anytls / Hysteria2 / Shadowsocks / tuic / WireGuard
Support Red Hat Enterprise Linux / Fedora Linux / Rocky Linux / AlmaLinux / CentOS
For more information, Please visit our website
https://github.com/2dust/v2rayN

%prep
%setup -q -n __PKGROOT__

%build

%install
install -dm0755 %{buildroot}/opt/v2rayN
cp -a * %{buildroot}/opt/v2rayN/

find %{buildroot}/opt/v2rayN -type d -exec chmod 0755 {} +
find %{buildroot}/opt/v2rayN -type f -exec chmod 0644 {} +
[ -f %{buildroot}/opt/v2rayN/v2rayN ] && chmod 0755 %{buildroot}/opt/v2rayN/v2rayN || :
[ -f %{buildroot}/opt/v2rayN/libSkiaSharp.so ] && chmod 0755 %{buildroot}/opt/v2rayN/libSkiaSharp.so || :
[ -f %{buildroot}/opt/v2rayN/libHarfBuzzSharp.so ] && chmod 0755 %{buildroot}/opt/v2rayN/libHarfBuzzSharp.so || :

install -dm0755 %{buildroot}%{_bindir}
install -m0755 /dev/stdin %{buildroot}%{_bindir}/v2rayn << 'EOF'
#!/usr/bin/bash
set -euo pipefail
DIR="/opt/v2rayN"
export LD_LIBRARY_PATH="$DIR:${LD_LIBRARY_PATH:-}"

if [[ -x "$DIR/v2rayN" ]]; then exec "$DIR/v2rayN" "$@"; fi

for dll in v2rayN.Desktop.dll v2rayN.dll; do
  if [[ -f "$DIR/$dll" ]]; then exec /usr/bin/dotnet "$DIR/$dll" "$@"; fi
done

echo "v2rayN launcher: no executable found in $DIR" >&2
ls -l "$DIR" >&2 || true
exit 1
EOF

install -dm0755 %{buildroot}%{_datadir}/applications
install -m0644 /dev/stdin %{buildroot}%{_datadir}/applications/v2rayn.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=v2rayN
Comment=v2rayN for Red Hat Enterprise Linux
Exec=v2rayn
Icon=v2rayn
Terminal=false
Categories=Network;
EOF

install -dm0755 %{buildroot}%{_datadir}/icons/hicolor/256x256/apps
install -m0644 %{_builddir}/__PKGROOT__/v2rayn.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/v2rayn.png

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

  sed -i "s/__VERSION__/${VERSION}/g" "$specfile"
  sed -i "s/__PKGROOT__/${PKGROOT}/g" "$specfile"
}

package_binary() {
  local short="$1"
  local rid="$2"
  local rpm_target="$3"
  local archdir="$4"
  local pubdir=""
  local workdir=""
  local specfile=""
  local sourcedir=""
  local specdir=""
  local project_dir=""
  local icon_candidate=""
  local f=""

  pubdir="$(dirname "$PROJECT")/bin/Release/${DOTNET_TFM}/${rid}/publish"
  [[ -d "$pubdir" ]] || { echo "Publish directory not found: $pubdir"; return 1; }

  workdir="$(mktemp -d)"
  trap '[[ -n "${workdir:-}" ]] && rm -rf "$workdir"' RETURN

  mkdir -p "$workdir/$PKGROOT"
  cp -a "$pubdir/." "$workdir/$PKGROOT/"

  copy_skiasharp_native_riscv64 "$workdir/$PKGROOT" || echo "[!] SkiaSharp native copy failed (skipped)"

  project_dir="$(cd "$(dirname "$PROJECT")" && pwd)"
  icon_candidate="$project_dir/v2rayN.png"
  [[ -f "$icon_candidate" ]] || { echo "Required icon not found: $icon_candidate"; return 1; }
  cp "$icon_candidate" "$workdir/$PKGROOT/v2rayn.png"

  stage_runtime_assets "$workdir/$PKGROOT" "$rid"

  rpmdev-setuptree
  sourcedir="${RPM_TOPDIR}/SOURCES"
  specdir="${RPM_TOPDIR}/SPECS"
  specfile="${specdir}/v2rayN.spec"

  mkdir -p "$sourcedir" "$specdir"
  tar -C "$workdir" -czf "$sourcedir/$PKGROOT.tar.gz" "$PKGROOT"

  write_spec_file "$specfile"
  rpmbuild -ba "$specfile" --target "$rpm_target"

  echo "Build done for $short. RPM at:"
  for f in "${RPM_TOPDIR}/RPMS/${archdir}/v2rayN-${VERSION}-1"*.rpm; do
    [[ -e "$f" ]] || continue
    echo "  $f"
    BUILT_RPMS+=("$f")
  done
}

select_targets() {
  printf '%s\n' riscv64
}

build_one_target() {
  local short="$1"
  local meta=()
  local rid=""
  local rpm_target=""
  local archdir=""

  mapfile -t meta < <(describe_target "$short") || return 1
  rid="${meta[0]}"
  rpm_target="${meta[1]}"
  archdir="${meta[2]}"

  echo "[*] Building for target: $short  (RID=$rid, RPM --target $rpm_target)"
  publish_binary "$rid"
  package_binary "$short" "$rid" "$rpm_target" "$archdir"
}

print_summary() {
  local rp=""

  echo ""
  echo "================ Build Summary ================"
  if [[ "${#BUILT_RPMS[@]}" -gt 0 ]]; then
    for rp in "${BUILT_RPMS[@]}"; do
      echo "$rp"
    done
  else
    echo "No RPMs detected in summary (check build logs above)."
  fi
  echo "=============================================="
}

main() {
  local targets=()
  local arch=""

  parse_args "$@"
  detect_environment
  install_dependencies
  prepare_workspace
  resolve_version
  apply_riscv_patch

  mapfile -t targets < <(select_targets)

  for arch in "${targets[@]}"; do
    build_one_target "$arch"
  done

  print_summary
}

main "$@"