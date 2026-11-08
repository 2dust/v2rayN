#!/usr/bin/env bash
set -euo pipefail

VERSION_ARG=""
WITH_CORE="both"
FORCE_NETCORE=0
ARCH_OVERRIDE=""
BUILD_FROM=""
XRAY_VER="${XRAY_VER:-}"
SING_VER="${SING_VER:-}"

MIN_KERNEL="6.12"
PKGROOT="v2rayN-publish"
PROJECT_HINT="v2rayN.Desktop/v2rayN.Desktop.csproj"
OUTPUT_DIR="${HOME}/debbuild"

NUGET_CACHE_DIR=""
DOTNET_NUPKG_BASE="${DOTNET_NUPKG_BASE:-https://github.com/autorepobot/dotnet-nupkg-prebuild/releases/download}"
SKIASHARP_BASE="${SKIASHARP_BASE:-https://github.com/autorepobot/SkiaSharp-ppc64le/releases/download}"
SkiaSharp_VERSION="${SkiaSharp_VERSION:-3.119.4}"
HarfBuzzSharp_VERSION="${HarfBuzzSharp_VERSION:-8.3.1.5}"

DOTNET_SDK_VERSION="${DOTNET_SDK_VERSION:-10.0.114}"

DOTNET_RISCV_VERSION="${DOTNET_RISCV_VERSION:-10.0.114}"
DOTNET_RISCV_BASE="${DOTNET_RISCV_BASE:-https://github.com/autorepobot/dotnet-riscv/releases/download}"
DOTNET_RISCV_FILE="dotnet-sdk-${DOTNET_RISCV_VERSION}-linux-riscv64.tar.gz"
DOTNET_RISCV_SDK_URL="${DOTNET_RISCV_SDK_URL:-${DOTNET_RISCV_BASE}/${DOTNET_RISCV_VERSION}/${DOTNET_RISCV_FILE}}"

DOTNET_PPC64LE_VERSION="${DOTNET_PPC64LE_VERSION:-10.0.114}"
DOTNET_PPC64LE_BASE="${DOTNET_PPC64LE_BASE:-https://github.com/IBM/dotnet-s390x/releases/download}"
DOTNET_PPC64LE_FILE="dotnet-sdk-${DOTNET_PPC64LE_VERSION}-linux-ppc64le.tar.gz"
DOTNET_PPC64LE_SDK_URL="${DOTNET_PPC64LE_SDK_URL:-${DOTNET_PPC64LE_BASE}/v${DOTNET_PPC64LE_VERSION}/${DOTNET_PPC64LE_FILE}}"

DOTNET_LOONGARCH_VERSION="${DOTNET_LOONGARCH_VERSION:-10.0.114}"
DOTNET_LOONGARCH_TAG="${DOTNET_LOONGARCH_TAG:-v10.0.114-loongarch64}"
DOTNET_LOONGARCH_BASE="${DOTNET_LOONGARCH_BASE:-https://github.com/loongson/dotnet/releases/download}"
DOTNET_LOONGARCH_FILE="dotnet-sdk-${DOTNET_LOONGARCH_VERSION}-linux-loongarch64.tar.gz"
DOTNET_LOONGARCH_SDK_URL="${DOTNET_LOONGARCH_SDK_URL:-${DOTNET_LOONGARCH_BASE}/${DOTNET_LOONGARCH_TAG}/${DOTNET_LOONGARCH_FILE}}"

OS_ID=""
OS_NAME=""
OS_VERSION_ID=""
HOST_ARCH=""
SCRIPT_DIR=""
PROJECT=""
VERSION=""
BUILT_ALL=0

declare -a BUILT_DEBS=()

die() {
  echo "$*" >&2
  exit 1
}

get_runtime_version() {
  local sdk_ver="$1"
  local prefix="${sdk_ver%.*}"
  local patch="${sdk_ver##*.}"
  echo "${prefix}.${patch#1}"
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
      --arch)        ARCH_OVERRIDE="${2:-}"; shift 2 ;;
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
    debian)
      echo "Detected supported system: ${OS_NAME:-$OS_ID} ${OS_VERSION_ID:-}"
      ;;
    *)
      die "Unsupported system: ${OS_NAME:-unknown} (${OS_ID:-unknown}).
This script only supports: Debian."
      ;;
  esac

  case "$HOST_ARCH" in
    x86_64|aarch64|riscv64|ppc64le|loongarch64) ;;
    *) die "Only supports x86_64 / aarch64 / riscv64 / ppc64le / loongarch64" ;;
  esac

  current_kernel="$(uname -r)"
  lowest="$(printf '%s\n%s\n' "$MIN_KERNEL" "$current_kernel" | sort -V | head -n1)"

  [[ "$lowest" == "$MIN_KERNEL" ]] || die "Kernel $current_kernel is below $MIN_KERNEL"
  echo "[OK] Kernel $current_kernel verified."
}

install_dependencies() {
  local install_ok=0
  local foreign_arch=""

  mkdir -p "$OUTPUT_DIR"

  if command -v apt-get >/dev/null 2>&1; then
    sudo apt-get update
    sudo apt-get -y install \
      curl unzip tar jq rsync ca-certificates git dpkg-dev fakeroot file \
      desktop-file-utils xdg-utils wget

    case "$HOST_ARCH" in
      aarch64) foreign_arch="amd64" ;;
      x86_64)  foreign_arch="arm64" ;;
      riscv64|ppc64le|loongarch64)
        foreign_arch=""
        ;;
      *)       die "Only supports x86_64 / aarch64 / riscv64 / ppc64le / loongarch64" ;;
    esac

    if [[ -n "$foreign_arch" ]]; then
      sudo dpkg --add-architecture "$foreign_arch" || true
      sudo apt-get update
      sudo apt-get -y install \
        "libc6:${foreign_arch}" \
        "libgcc-s1:${foreign_arch}" \
        "libstdc++6:${foreign_arch}" \
        "zlib1g:${foreign_arch}" \
        "libfontconfig1:${foreign_arch}"
    fi

    case "$HOST_ARCH" in
      x86_64|aarch64)
        wget -q https://dot.net/v1/dotnet-install.sh
        chmod +x dotnet-install.sh
        ./dotnet-install.sh --version "$DOTNET_SDK_VERSION" --install-dir "$HOME/.dotnet"

        export PATH="$HOME/.dotnet:$PATH"
        export DOTNET_ROOT="$HOME/.dotnet"

        dotnet --info >/dev/null 2>&1 && install_ok=1
        ;;
      riscv64|ppc64le|loongarch64)
        install_ok=1
        ;;
    esac
  fi

  if [[ "$install_ok" -ne 1 ]]; then
    echo "Could not auto-install dependencies for '$OS_ID'. Make sure these are available:"
    echo "dotnet-sdk 10.x, curl, unzip, tar, rsync, git, dpkg-deb, desktop-file-utils, xdg-utils"
    exit 1
  fi
}

prepare_deb_target_arch() {
  local short="$1"
  local target_arch=""

  case "$HOST_ARCH:$short" in
    x86_64:ppc64le|aarch64:ppc64le)
      target_arch="ppc64el"
      ;;
    x86_64:riscv64|aarch64:riscv64)
      target_arch="riscv64"
      ;;
    x86_64:loong64|aarch64:loong64)
      target_arch="loong64"
      ;;
    *)
      return 0
      ;;
  esac

  echo "[+] Prepare Debian multiarch environment for $short: $target_arch"

  sudo dpkg --add-architecture "$target_arch" || true

  if [[ "$target_arch" == "loong64" ]]; then
    echo "[+] Add Loong13 Debian Trixie source for loong64"

    cat <<'EOF' | sudo tee /etc/apt/sources.list.d/loong64-loong13.list >/dev/null
deb [arch=loong64] http://loong13.debian.net/debian-loong64/ trixie main contrib non-free non-free-firmware
deb [arch=loong64] http://loong13.debian.net/debian-loong64-security/ trixie-security main contrib non-free non-free-firmware
EOF

    echo "[+] Install Loong13 archive keyring"

    tmp_keyring="$(mktemp -d)"
    curl -fL \
      "http://loong13.debian.net/debian-loong64/pool/main/d/debian-loong64-non-official-archive-keyring/debian-loong64-non-official-archive-keyring_2025.12.22_all.deb" \
      -o "$tmp_keyring/loong13-keyring.deb"

    sudo dpkg -i "$tmp_keyring/loong13-keyring.deb"
    rm -rf "$tmp_keyring"
  fi

  sudo apt-get update

  sudo apt-get -y install \
    "libc6:${target_arch}" \
    "libgcc-s1:${target_arch}" \
    "libstdc++6:${target_arch}" \
    "zlib1g:${target_arch}" \
    "libfontconfig1:${target_arch}" \
    "libfreetype6:${target_arch}"

  echo "[OK] Debian target libraries prepared for $target_arch"
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

download_nuget_packages() {
  NUGET_CACHE_DIR="$(mktemp -d)"

  local riscv_rt ppc64le_rt loong_rt
  riscv_rt="$(get_runtime_version "$DOTNET_RISCV_VERSION")"
  ppc64le_rt="$(get_runtime_version "$DOTNET_PPC64LE_VERSION")"
  loong_rt="$(get_runtime_version "$DOTNET_LOONGARCH_VERSION")"

  local urls=(
    "${DOTNET_NUPKG_BASE}/${loong_rt}/Microsoft.AspNetCore.App.Runtime.linux-loongarch64.${loong_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${ppc64le_rt}/Microsoft.AspNetCore.App.Runtime.linux-ppc64le.${ppc64le_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${riscv_rt}/Microsoft.AspNetCore.App.Runtime.linux-riscv64.${riscv_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${loong_rt}/Microsoft.NETCore.App.Host.linux-loongarch64.${loong_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${ppc64le_rt}/Microsoft.NETCore.App.Host.linux-ppc64le.${ppc64le_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${riscv_rt}/Microsoft.NETCore.App.Host.linux-riscv64.${riscv_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${loong_rt}/Microsoft.NETCore.App.Runtime.linux-loongarch64.${loong_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${ppc64le_rt}/Microsoft.NETCore.App.Runtime.linux-ppc64le.${ppc64le_rt}.nupkg"
    "${DOTNET_NUPKG_BASE}/${riscv_rt}/Microsoft.NETCore.App.Runtime.linux-riscv64.${riscv_rt}.nupkg"
    "${SKIASHARP_BASE}/v${SkiaSharp_VERSION}/SkiaSharp.NativeAssets.Linux.${SkiaSharp_VERSION}.nupkg"
    "${SKIASHARP_BASE}/v${SkiaSharp_VERSION}/HarfBuzzSharp.NativeAssets.Linux.${HarfBuzzSharp_VERSION}.nupkg"
  )

  echo "[+] Downloading prebuilt NuGet packages to: $NUGET_CACHE_DIR"
  local u pkg_name
  for u in "${urls[@]}"; do
    pkg_name="${u##*/}"
    echo "  -> $pkg_name"
    curl -fL "$u" -o "$NUGET_CACHE_DIR/$pkg_name" || {
      echo "[!] Failed to download $u" >&2
      rm -rf "$NUGET_CACHE_DIR"
      NUGET_CACHE_DIR=""
      return 1
    }
  done

  echo "[OK] Prebuilt NuGet packages ready in $NUGET_CACHE_DIR"
}

prepare_dotnet_sdk() {
  local short="$1"
  local sdk_url=""
  local sdk_file=""
  local sdk_dir=""
  local tmp=""

  case "$HOST_ARCH:$short" in
    x86_64:x64|aarch64:arm64)
      return 0
      ;;
    riscv64:riscv64)
      sdk_url="$DOTNET_RISCV_SDK_URL"
      sdk_file="$DOTNET_RISCV_FILE"
      ;;
    ppc64le:ppc64le)
      sdk_url="$DOTNET_PPC64LE_SDK_URL"
      sdk_file="$DOTNET_PPC64LE_FILE"
      ;;
    loongarch64:loong64)
      sdk_url="$DOTNET_LOONGARCH_SDK_URL"
      sdk_file="$DOTNET_LOONGARCH_FILE"
      ;;
    *)
      return 0
      ;;
  esac

  sdk_dir="$(mktemp -d)"
  tmp="$(mktemp -d)"

  echo "[+] Download .NET SDK for $short: $sdk_url"

  curl -fL "$sdk_url" -o "$tmp/$sdk_file" || {
    rm -rf "$tmp" "$sdk_dir"
    return 1
  }

  tar -C "$sdk_dir" -xzf "$tmp/$sdk_file" || {
    rm -rf "$tmp" "$sdk_dir"
    return 1
  }

  rm -rf "$tmp"

  [[ -x "$sdk_dir/dotnet" ]] || {
    rm -rf "$sdk_dir"
    return 1
  }

  export DOTNET_ROOT="$sdk_dir"
  export PATH="$DOTNET_ROOT:$PATH"
  hash -r

  echo "[OK] .NET SDK for $short extracted to: $sdk_dir"
  dotnet --info

  DOTNET_SDK_DIR="$sdk_dir"
}

xray_url_for_rid() {
  local rid="$1"
  local ver="$2"

  case "$rid" in
    linux-x64)         echo "https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-64.zip" ;;
    linux-arm64)       echo "https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-arm64-v8a.zip" ;;
    linux-riscv64)     echo "https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-riscv64.zip" ;;
    linux-ppc64le)     echo "https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-ppc64le.zip" ;;
    linux-loongarch64) echo "https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-loong64.zip" ;;
    *)                 return 1 ;;
  esac
}

singbox_url_for_rid() {
  local rid="$1"
  local ver="$2"

  case "$rid" in
    linux-x64)         echo "https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-amd64.tar.gz" ;;
    linux-arm64)       echo "https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-arm64.tar.gz" ;;
    linux-riscv64)     echo "https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-riscv64.tar.gz" ;;
    linux-ppc64le)     echo "https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-ppc64le.tar.gz" ;;
    linux-loongarch64) echo "https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-loong64.tar.gz" ;;
    *)                 return 1 ;;
  esac
}

bundle_url_for_rid() {
  local rid="$1"

  case "$rid" in
    linux-x64)         echo "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-64.zip" ;;
    linux-arm64)       echo "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-arm64.zip" ;;
    linux-riscv64)     echo "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-riscv64.zip" ;;
    linux-ppc64le)     echo "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-ppc64le.zip" ;;
    linux-loongarch64) echo "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-loong64.zip" ;;
    *)                 return 1 ;;
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
  url="$(xray_url_for_rid "$rid" "$ver")" || { echo "[xray] Unsupported RID for upstream Xray: $rid (skipped)"; return 0; }

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
    x64)     printf '%s\n%s\n' "linux-x64" "amd64" ;;
    arm64)   printf '%s\n%s\n' "linux-arm64" "arm64" ;;
    riscv64) printf '%s\n%s\n' "linux-riscv64" "riscv64" ;;
    ppc64le) printf '%s\n%s\n' "linux-ppc64le" "ppc64el" ;;
    loong64) printf '%s\n%s\n' "linux-loongarch64" "loong64" ;;
    *)       echo "Unknown arch '$short' (use x64|arm64|riscv64|ppc64le|loong64)" >&2; return 1 ;;
  esac
}

publish_binary() {
  local rid="$1"

  dotnet clean "$PROJECT" -c Release
  rm -rf "$(dirname "$PROJECT")/bin/Release/net10.0" || true
  dotnet restore "$PROJECT" \
    -r "$rid" \
    --source "$NUGET_CACHE_DIR" \
    --source "https://api.nuget.org/v3/index.json"
  dotnet publish "$PROJECT" \
    -c Release \
    -r "$rid" \
    --source "$NUGET_CACHE_DIR" \
    --source "https://api.nuget.org/v3/index.json" \
    -p:PublishSingleFile=false \
    -p:SelfContained=true \
    --no-restore
}

write_launcher_file() {
  local stage="$1"

  install -m 755 /dev/stdin "$stage/usr/bin/v2rayn" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
DIR="/opt/v2rayN"
cd "$DIR"

if [[ -x "$DIR/v2rayN" ]]; then
  exec "$DIR/v2rayN" "$@"
fi

for dll in v2rayN.Desktop.dll v2rayN.dll; do
  if [[ -f "$DIR/$dll" ]]; then
    exec /usr/bin/dotnet "$DIR/$dll" "$@"
  fi
done

echo "v2rayN launcher: no executable found in $DIR" >&2
ls -l "$DIR" >&2 || true
exit 1
EOF
}

write_desktop_file() {
  local stage="$1"

  install -m 644 /dev/stdin "$stage/usr/share/applications/v2rayn.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=v2rayN
Comment=v2rayN for Debian GNU Linux
Exec=v2rayn
Icon=v2rayn
Terminal=false
Categories=Network;
EOF
}

write_maintainer_scripts() {
  local debian_dir="$1"

  install -m 755 /dev/stdin "$debian_dir/postinst" <<'EOF'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f /usr/share/icons/hicolor >/dev/null 2>&1 || true
fi
exit 0
EOF

  install -m 755 /dev/stdin "$debian_dir/postrm" <<'EOF'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f /usr/share/icons/hicolor >/dev/null 2>&1 || true
fi
exit 0
EOF
}

package_binary() {
  local short="$1"
  local rid="$2"
  local deb_arch="$3"
  local pubdir=""
  local workdir=""
  local stage=""
  local debian_dir=""
  local project_dir=""
  local icon_candidate=""
  local shlibs_depends=""
  local extra_depends=""
  local final_depends=""
  local multiarch=""
  local sys_libdir=""
  local sys_usrlibdir=""
  local deb_out=""

  pubdir="$(dirname "$PROJECT")/bin/Release/net10.0/${rid}/publish"
  [[ -d "$pubdir" ]] || { echo "Publish directory not found: $pubdir"; return 1; }

  workdir="$(mktemp -d)"
  trap '[[ -n "${workdir:-}" ]] && rm -rf "$workdir"' RETURN

  stage="$workdir/${PKGROOT}_${VERSION}_${deb_arch}"
  debian_dir="$stage/DEBIAN"

  mkdir -p "$stage/opt/v2rayN" "$stage/usr/bin" "$stage/usr/share/applications" "$stage/usr/share/icons/hicolor/256x256/apps" "$debian_dir"
  cp -a "$pubdir/." "$stage/opt/v2rayN/"

  project_dir="$(cd "$(dirname "$PROJECT")" && pwd)"
  icon_candidate="$project_dir/v2rayN.png"
  [[ -f "$icon_candidate" ]] && cp "$icon_candidate" "$stage/usr/share/icons/hicolor/256x256/apps/v2rayn.png" || true

  stage_runtime_assets "$stage/opt/v2rayN" "$rid"
  write_launcher_file "$stage"
  write_desktop_file "$stage"
  write_maintainer_scripts "$debian_dir"

  prepare_deb_target_arch "$short"

  extra_depends="libc6 (>= 2.39), fontconfig (>= 2.15.0), desktop-file-utils (>= 0.26), xdg-utils (>= 1.1.3), coreutils (>= 9.4), bash (>= 5.2.21), libfreetype6 (>= 2.13)"

  mkdir -p "$workdir/debian"
  cat > "$workdir/debian/control" <<EOF
Source: v2rayn
Section: net
Priority: optional
Maintainer: 2dust <noreply@github.com>
Standards-Version: 4.7.0

Package: v2rayn
Architecture: ${deb_arch}
Description: v2rayN
EOF

  multiarch="$(dpkg-architecture -a"$deb_arch" -qDEB_HOST_MULTIARCH)"
  sys_libdir="/lib/$multiarch"
  sys_usrlibdir="/usr/lib/$multiarch"

  : > "$debian_dir/substvars"

  mapfile -t ELF_FILES < <(
    find "$stage/opt/v2rayN" -type f \( -name "*.so*" -o -perm -111 \) ! -name 'libcoreclrtraceptprovider.so'
  )

  if [[ "${#ELF_FILES[@]}" -gt 0 ]]; then
    (
      cd "$workdir"
      dpkg-shlibdeps \
        -l"$stage/opt/v2rayN" \
        -l"$sys_libdir" \
        -l"$sys_usrlibdir" \
        -T"$debian_dir/substvars" \
        "${ELF_FILES[@]}"
    ) || true
  fi

  shlibs_depends="$(sed -n 's/^shlibs:Depends=//p' "$debian_dir/substvars" | head -n1 || true)"

  if [[ -n "$shlibs_depends" ]]; then
    shlibs_depends="$(echo "$shlibs_depends" \
      | sed -E 's/ *\([^)]*\)//g' \
      | sed -E 's/ *, */, /g' \
      | sed -E 's/^, *//; s/, *$//')"
    final_depends="${shlibs_depends}, ${extra_depends}"
  else
    final_depends="${extra_depends}"
  fi

  cat > "$debian_dir/control" <<EOF
Package: v2rayn
Version: ${VERSION}
Architecture: ${deb_arch}
Maintainer: 2dust <noreply@github.com>
Homepage: https://github.com/2dust/v2rayN
Section: net
Priority: optional
Depends: ${final_depends}
Description: v2rayN (Avalonia) GUI client for Linux
 Support vless / vmess / Trojan / http / socks / Anytls / Hysteria2 /
 Shadowsocks / tuic / WireGuard.
EOF

  find "$stage/opt/v2rayN" -type d -exec chmod 0755 {} +
  find "$stage/opt/v2rayN" -type f -exec chmod 0644 {} +
  [[ -f "$stage/opt/v2rayN/v2rayN" ]] && chmod 0755 "$stage/opt/v2rayN/v2rayN" || true

  deb_out="$OUTPUT_DIR/v2rayn_${VERSION}_${deb_arch}.deb"
  dpkg-deb --root-owner-group --build "$stage" "$deb_out"

  echo "Build done for $short. DEB at:"
  echo "  $deb_out"
  BUILT_DEBS+=("$deb_out")
}

select_targets() {
  case "${ARCH_OVERRIDE:-}" in
    all)                 printf '%s\n' x64 arm64 riscv64 ppc64le loong64 ;;
    x64|amd64)           printf '%s\n' x64 ;;
    arm64|aarch64)       printf '%s\n' arm64 ;;
    riscv64)             printf '%s\n' riscv64 ;;
    ppc64le|ppc64el)     printf '%s\n' ppc64le ;;
    loong64|loongarch64) printf '%s\n' loong64 ;;
    "")
      case "$HOST_ARCH" in
        x86_64)  printf '%s\n' x64 ;;
        aarch64) printf '%s\n' arm64 ;;
        *)       return 1 ;;
      esac
      ;;
    *)
      echo "Unknown --arch '${ARCH_OVERRIDE}'. Use x64|arm64|riscv64|ppc64le|loong64|all." >&2
      return 1
      ;;
  esac
}

build_one_target() {
  local short="$1"
  local meta=()
  local rid=""
  local deb_arch=""

  mapfile -t meta < <(describe_target "$short") || return 1
  rid="${meta[0]}"
  deb_arch="${meta[1]}"

  echo "[*] Building for target: $short  (RID=$rid, DEB arch=$deb_arch)"
  prepare_dotnet_sdk "$short" || die "Failed to prepare .NET SDK for $short."
  publish_binary "$rid"
  package_binary "$short" "$rid" "$deb_arch"
}

print_summary() {
  local pkg=""

  echo ""
  echo "================ Build Summary ================="
  if [[ "${#BUILT_DEBS[@]}" -gt 0 ]]; then
    echo "Output directory: $OUTPUT_DIR"
    for pkg in "${BUILT_DEBS[@]}"; do
      echo "$pkg"
    done
  else
    echo "No DEBs detected in summary (check build logs above)."
  fi
  echo "==============================================="
}

main() {
  local targets=()
  local arch=""

  parse_args "$@"
  detect_environment
  install_dependencies
  prepare_workspace
  resolve_version

  download_nuget_packages || die "Failed to download prebuilt NuGet packages."

  mapfile -t targets < <(select_targets)
  [[ "${ARCH_OVERRIDE:-}" == "all" ]] && BUILT_ALL=1 || BUILT_ALL=0

  for arch in "${targets[@]}"; do
    build_one_target "$arch"
  done

  rm -rf "${NUGET_CACHE_DIR:-}" || true
  rm -rf "${DOTNET_SDK_DIR:-}" || true

  print_summary
}

main "$@"
