#!/usr/bin/env bash
set -euo pipefail

# == Require Red Hat Enterprise Linux/FedoraLinux/RockyLinux/AlmaLinux/CentOS OR Ubuntu/Debian ==
if [[ -r /etc/os-release ]]; then
  . /etc/os-release
  case "$ID" in
    rhel|rocky|almalinux|fedora|centos|ubuntu|debian)
      echo "[OK] Detected supported system: $NAME $VERSION_ID"
      ;;
    *)
      echo "[ERROR] Unsupported system: $NAME ($ID)."
      echo "This script only supports Red Hat Enterprise Linux/RockyLinux/AlmaLinux/CentOS or Ubuntu/Debian."
      exit 1
      ;;
  esac
else
  echo "[ERROR] Cannot detect system (missing /etc/os-release)."
  exit 1
fi

# ===== Config & Parse arguments =========================================================
VERSION_ARG="${1:-}"     # Pass version number like 7.13.8, or leave empty
WITH_CORE="both"         # Default: bundle both xray+sing-box
AUTOSTART=0              # 1 = enable system-wide autostart (/etc/xdg/autostart)
FORCE_NETCORE=0          # --netcore => skip archive bundle, use separate downloads
ARCH_OVERRIDE=""         # --arch x64|arm64|all (optional compile target)
BUILD_FROM=""            # --buildfrom 1|2|3 to select channel non-interactively

# If the first argument starts with --, do not treat it as a version number
if [[ "${VERSION_ARG:-}" == --* ]]; then
  VERSION_ARG=""
fi
# Take the first non --* argument as version, discard it
if [[ -n "${VERSION_ARG:-}" ]]; then shift || true; fi

# Parse remaining optional arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --with-core)     WITH_CORE="${2:-both}"; shift 2;;
    --autostart)     AUTOSTART=1; shift;;
    --xray-ver)      XRAY_VER="${2:-}"; shift 2;;
    --singbox-ver)   SING_VER="${2:-}"; shift 2;;
    --netcore)       FORCE_NETCORE=1; shift;;
    --arch)          ARCH_OVERRIDE="${2:-}"; shift 2;;
    --buildfrom)     BUILD_FROM="${2:-}"; shift 2;;
    *)
      if [[ -z "${VERSION_ARG:-}" ]]; then VERSION_ARG="$1"; fi
      shift;;
  esac
done

# Conflict: version number AND --buildfrom cannot be used together
if [[ -n "${VERSION_ARG:-}" && -n "${BUILD_FROM:-}" ]]; then
  echo "[ERROR] You cannot specify both an explicit version and --buildfrom at the same time."
  echo "        Provide either a version (e.g. 7.14.0) OR --buildfrom 1|2|3."
  exit 1
fi

# ===== Environment check + Dependencies ========================================
host_arch="$(uname -m)"
[[ "$host_arch" == "aarch64" || "$host_arch" == "x86_64" ]] || { echo "Only supports aarch64 / x86_64"; exit 1; }

install_ok=0
case "$ID" in
  # ------------------------------ RHEL family (UNCHANGED) ------------------------------
  rhel|rocky|almalinux|centos)
    if command -v dnf >/dev/null 2>&1; then
      sudo dnf -y install dotnet-sdk-8.0 rpm-build rpmdevtools curl unzip tar rsync || \
      sudo dnf -y install dotnet-sdk rpm-build rpmdevtools curl unzip tar rsync
      install_ok=1
    elif command -v yum >/dev/null 2>&1; then
      sudo yum -y install dotnet-sdk-8.0 rpm-build rpmdevtools curl unzip tar rsync || \
      sudo yum -y install dotnet-sdk rpm-build rpmdevtools curl unzip tar rsync
      install_ok=1
    fi
    ;;
  # ------------------------------ Ubuntu ----------------------------------------------
  ubuntu)
    sudo apt-get update
    # Ensure 'universe' (Ubuntu) to get 'rpm'
    if ! apt-cache policy | grep -q '^500 .*ubuntu.com/ubuntu.* universe'; then
      sudo apt-get -y install software-properties-common || true
      sudo add-apt-repository -y universe || true
      sudo apt-get update
    fi
    # Base tools + rpm (provides rpmbuild)
    sudo apt-get -y install curl unzip tar rsync rpm || true
    # Cross-arch binutils so strip matches target arch + objdump for brp scripts
    sudo apt-get -y install binutils binutils-x86-64-linux-gnu binutils-aarch64-linux-gnu || true
    # rpmbuild presence check
    if ! command -v rpmbuild >/dev/null 2>&1; then
      echo "[ERROR] 'rpmbuild' not found after installing 'rpm'."
      echo "        Please ensure the 'rpm' package is available from your repos (universe on Ubuntu)."
      exit 1
    fi
    # .NET SDK 8 (best effort via apt)
    if ! command -v dotnet >/dev/null 2>&1; then
      sudo apt-get -y install dotnet-sdk-8.0 || true
      sudo apt-get -y install dotnet-sdk-8 || true
      sudo apt-get -y install dotnet-sdk || true
    fi
    install_ok=1
    ;;
  # ------------------------------ Debian (KEEP, with local dotnet install) ------------
  debian)
    sudo apt-get update
    # Base tools + rpm (provides rpmbuild on Debian) + objdump/strip
    sudo apt-get -y install curl unzip tar rsync rpm binutils || true
    # rpmbuild presence check
    if ! command -v rpmbuild >/dev/null 2>&1; then
      echo "[ERROR] 'rpmbuild' not found after installing 'rpm'."
      echo "        Please ensure 'rpm' is available from Debian repos."
      exit 1
    fi
    # Try apt for dotnet; fallback to official installer into $HOME/.dotnet
    if ! command -v dotnet >/dev/null 2>&1; then
      echo "[INFO] 'dotnet' not found. Installing .NET 8 SDK locally to \$HOME/.dotnet ..."
      tmp="$(mktemp -d)"; trap '[[ -n "${tmp:-}" ]] && rm -rf "$tmp"' RETURN
      curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tmp/dotnet-install.sh"
      bash "$tmp/dotnet-install.sh" --channel 8.0 --install-dir "$HOME/.dotnet"
      export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
      export DOTNET_ROOT="$HOME/.dotnet"
      if ! command -v dotnet >/dev/null 2>&1; then
        echo "[ERROR] dotnet installation failed."
        exit 1
      fi
    fi
    install_ok=1
    ;;
esac

if [[ "$install_ok" -ne 1 ]]; then
  echo "[WARN] Could not auto-install dependencies for '$ID'. Make sure these are available:"
  echo "       dotnet-sdk 8.x, curl, unzip, tar, rsync, rpm, rpmdevtools, rpm-build (on RPM-based distros)"
fi

command -v curl >/dev/null

# Root directory = the script's location
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Git submodules (best effort)
if [[ -f .gitmodules ]]; then
  git submodule sync --recursive || true
  git submodule update --init --recursive || true
fi

# ===== Locate project ================================================================
PROJECT="v2rayN.Desktop/v2rayN.Desktop.csproj"
if [[ ! -f "$PROJECT" ]]; then
  PROJECT="$(find . -maxdepth 3 -name 'v2rayN.Desktop.csproj' | head -n1 || true)"
fi
[[ -f "$PROJECT" ]] || { echo "v2rayN.Desktop.csproj not found"; exit 1; }

# ===== Resolve GUI version & auto checkout ============================================
VERSION=""

choose_channel() {
  # If --buildfrom provided, map it directly and skip interaction.
  if [[ -n "${BUILD_FROM:-}" ]]; then
    case "$BUILD_FROM" in
      1) echo "latest"; return 0;;
      2) echo "prerelease"; return 0;;
      3) echo "keep"; return 0;;
      *) echo "[ERROR] Invalid --buildfrom value: ${BUILD_FROM}. Use 1|2|3." >&2; exit 1;;
    esac
  fi

  # Print menu to stderr and read from /dev/tty so stdout only carries the token.
  local ch="latest" sel=""
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
        *) ch="latest" ;;
      esac
    else
      ch="latest"
    fi
  else
    ch="latest"
  fi
  echo "$ch"
}

get_latest_tag_latest() {
  # Resolve /releases/latest → tag_name
  curl -fsSL "https://api.github.com/repos/2dust/v2rayN/releases/latest" \
    | grep -Eo '"tag_name":\s*"v?[^"]+"' \
    | head -n1 \
    | sed -E 's/.*"tag_name":\s*"v?([^"]+)".*/\1/'
}

get_latest_tag_prerelease() {
  # Resolve newest prerelease=true tag; prefer jq, fallback to sed/grep (no awk)
  local json tag
  json="$(curl -fsSL "https://api.github.com/repos/2dust/v2rayN/releases?per_page=20")" || return 1

  # 1) Use jq if present
  if command -v jq >/dev/null 2>&1; then
    tag="$(printf '%s' "$json" \
      | jq -r '[.[] | select(.prerelease==true)][0].tag_name' 2>/dev/null \
      | sed 's/^v//')" || true
  fi

  # 2) Fallback to sed/grep only
  if [[ -z "${tag:-}" || "${tag:-}" == "null" ]]; then
    tag="$(printf '%s' "$json" \
      | tr '\n' ' ' \
      | sed 's/},[[:space:]]*{/\n/g' \
      | grep -m1 -E '"prerelease"[[:space:]]*:[[:space:]]*true' \
      | grep -Eo '"tag_name"[[:space:]]*:[[:space:]]*"v?[^"]+"' \
      | head -n1 \
      | sed -E 's/.*"tag_name"[[:space:]]*:[[:space:]]*"v?([^"]+)".*/\1/')" || true
  fi

  [[ -n "${tag:-}" && "${tag:-}" != "null" ]] || return 1
  printf '%s\n' "$tag"
}

git_try_checkout() {
  # Try a series of refs and checkout when found.
  local want="$1" ref=""
  if git rev-parse --git-dir >/dev/null 2>&1; then
    git fetch --tags --force --prune --depth=1 || true
    if git rev-parse "refs/tags/v${want}" >/dev/null 2>&1; then
      ref="v${want}"
    elif git rev-parse "refs/tags/${want}" >/dev/null 2>&1; then
      ref="${want}"
    elif git rev-parse --verify "${want}" >/dev/null 2>&1; then
      ref="${want}"
    fi
    if [[ -n "$ref" ]]; then
      echo "[OK] Found ref '${ref}', checking out..."
      git checkout -f "${ref}"
      if [[ -f .gitmodules ]]; then
        git submodule sync --recursive || true
        git submodule update --init --recursive || true
      fi
      return 0
    fi
  fi
  return 1
}

if git rev-parse --git-dir >/dev/null 2>&1; then
  if [[ -n "${VERSION_ARG:-}" ]]; then
    echo "[*] Trying to switch v2rayN repo to version: ${VERSION_ARG}"
    if git_try_checkout "${VERSION_ARG#v}"; then
      VERSION="${VERSION_ARG#v}"
    else
      echo "[WARN] Tag '${VERSION_ARG}' not found."
      ch="$(choose_channel)"
      if [[ "$ch" == "keep" ]]; then
        echo "[*] Keep current repository state (no checkout)."
        if git describe --tags --abbrev=0 >/dev/null 2>&1; then
          VERSION="$(git describe --tags --abbrev=0)"
        else
          VERSION="0.0.0+git"
        fi
        VERSION="${VERSION#v}"
      else
        echo "[*] Resolving ${ch} tag from GitHub releases..."
        tag=""
        if [[ "$ch" == "prerelease" ]]; then
          tag="$(get_latest_tag_prerelease || true)"
          if [[ -z "$tag" ]]; then
            echo "[WARN] Failed to resolve prerelease tag, falling back to latest."
            tag="$(get_latest_tag_latest || true)"
          fi
        else
          tag="$(get_latest_tag_latest || true)"
        fi
        [[ -n "$tag" ]] || { echo "[ERROR] Failed to resolve latest tag for channel '${ch}'."; exit 1; }
        echo "[*] Latest tag for '${ch}': ${tag}"
        git_try_checkout "$tag" || { echo "[ERROR] Failed to checkout '${tag}'."; exit 1; }
        VERSION="${tag#v}"
      fi
    fi
  else
    ch="$(choose_channel)"
    if [[ "$ch" == "keep" ]]; then
      echo "[*] Keep current repository state (no checkout)."
      if git describe --tags --abbrev=0 >/dev/null 2>&1; then
        VERSION="$(git describe --tags --abbrev=0)"
      else
        VERSION="0.0.0+git"
      fi
      VERSION="${VERSION#v}"
    else
      echo "[*] Resolving ${ch} tag from GitHub releases..."
      tag=""
      if [[ "$ch" == "prerelease" ]]; then
        tag="$(get_latest_tag_prerelease || true)"
        if [[ -z "$tag" ]]; then
          echo "[WARN] Failed to resolve prerelease tag, falling back to latest."
          tag="$(get_latest_tag_latest || true)"
        fi
      else
        tag="$(get_latest_tag_latest || true)"
      fi
      [[ -n "$tag" ]] || { echo "[ERROR] Failed to resolve latest tag for channel '${ch}'."; exit 1; }
      echo "[*] Latest tag for '${ch}': ${tag}"
      git_try_checkout "$tag" || { echo "[ERROR] Failed to checkout '${tag}'."; exit 1; }
      VERSION="${tag#v}"
    fi
  fi
else
  echo "[WARN] Current directory is not a git repo; cannot checkout version. Proceeding on current tree."
  VERSION="${VERSION_ARG:-}"
  if [[ -z "$VERSION" ]]; then
    if git describe --tags --abbrev=0 >/dev/null 2>&1; then
      VERSION="$(git describe --tags --abbrev=0)"
    else
      VERSION="0.0.0+git"
    fi
  fi
  VERSION="${VERSION#v}"
fi
echo "[*] GUI version resolved as: ${VERSION}"

# ===== Helpers for core/rules download (use RID_DIR for arch sync) =====================
download_xray() {
  # Download Xray core and install to outdir/xray
  local outdir="$1" ver="${XRAY_VER:-}" url tmp zipname="xray.zip"
  mkdir -p "$outdir"
  if [[ -n "${XRAY_VER:-}" ]]; then ver="${XRAY_VER}"; fi
  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/XTLS/Xray-core/releases/latest \
        | grep -Eo '"tag_name":\s*"v[^"]+"' | sed -E 's/.*"v([^"]+)".*/\1/' | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[xray] Failed to get version"; return 1; }
  if [[ "$RID_DIR" == "linux-arm64" ]]; then
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-arm64-v8a.zip"
  else
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-64.zip"
  fi
  echo "[+] Download xray: $url"
  tmp="$(mktemp -d)"; trap '[[ -n "${tmp:-}" ]] && rm -rf "$tmp"' RETURN
  curl -fL "$url" -o "$tmp/$zipname"
  unzip -q "$tmp/$zipname" -d "$tmp"
  install -Dm755 "$tmp/xray" "$outdir/xray"
}

download_singbox() {
  # Download sing-box core and install to outdir/sing-box
  local outdir="$1" ver="${SING_VER:-}" url tmp tarname="singbox.tar.gz" bin
  mkdir -p "$outdir"
  if [[ -n "${SING_VER:-}" ]]; then ver="${SING_VER}"; fi
  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/SagerNet/sing-box/releases/latest \
        | grep -Eo '"tag_name":\s*"v[^"]+"' | sed -E 's/.*"v([^"]+)".*/\1/' | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[sing-box] Failed to get version"; return 1; }
  if [[ "$RID_DIR" == "linux-arm64" ]]; then
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-arm64.tar.gz"
  else
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-amd64.tar.gz"
  fi
  echo "[+] Download sing-box: $url"
  tmp="$(mktemp -d)"; trap '[[ -n "${tmp:-}" ]] && rm -rf "$tmp"' RETURN
  curl -fL "$url" -o "$tmp/$tarname"
  tar -C "$tmp" -xzf "$tmp/$tarname"
  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box unpack failed"; return 1; }
  install -Dm755 "$bin" "$outdir/sing-box"
}

# ---- NEW: download_mihomo (REQUIRED in --netcore mode) ----
download_mihomo() {
  # Download mihomo into outroot/bin/mihomo/mihomo
  local outroot="$1"
  local url=""
  if [[ "$RID_DIR" == "linux-arm64" ]]; then
    url="https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-arm64/bin/mihomo/mihomo"
  else
    url="https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-64/bin/mihomo/mihomo"
  fi
  echo "[+] Download mihomo: $url"
  mkdir -p "$outroot/bin/mihomo"
  curl -fL "$url" -o "$outroot/bin/mihomo/mihomo"
  chmod +x "$outroot/bin/mihomo/mihomo" || true
}

# Move geo files to a unified path: outroot/bin
unify_geo_layout() {
  local outroot="$1"
  mkdir -p "$outroot/bin"
  local names=( \
    "geosite.dat" \
    "geoip.dat" \
    "geoip-only-cn-private.dat" \
    "Country.mmdb" \
    "geoip.metadb" \
  )
  for n in "${names[@]}"; do
    # If file exists under bin/xray/, move it up to bin/
    if [[ -f "$outroot/bin/xray/$n" ]]; then
      mv -f "$outroot/bin/xray/$n" "$outroot/bin/$n"
    fi
    # If file already in bin/, leave it as-is
    if [[ -f "$outroot/bin/$n" ]]; then
      :
    fi
  done
}

# Download geo/rule assets; then unify to bin/
download_geo_assets() {
  local outroot="$1"
  local bin_dir="$outroot/bin"
  local srss_dir="$bin_dir/srss"
  mkdir -p "$bin_dir" "$srss_dir"

  echo "[+] Download Xray Geo to ${bin_dir}"
  curl -fsSL -o "$bin_dir/geosite.dat" \
    "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geosite.dat"
  curl -fsSL -o "$bin_dir/geoip.dat" \
    "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geoip.dat"
  curl -fsSL -o "$bin_dir/geoip-only-cn-private.dat" \
    "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/geoip-only-cn-private.dat"
  curl -fsSL -o "$bin_dir/Country.mmdb" \
    "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb"

  echo "[+] Download sing-box rule DB & rule-sets"
  curl -fsSL -o "$bin_dir/geoip.metadb" \
    "https://github.com/MetaCubeX/meta-rules-dat/releases/latest/download/geoip.metadb" || true

  for f in \
    geoip-private.srs geoip-cn.srs geoip-facebook.srs geoip-fastly.srs \
    geoip-google.srs geoip-netflix.srs geoip-telegram.srs geoip-twitter.srs; do
    curl -fsSL -o "$srss_dir/$f" \
      "https://raw.githubusercontent.com/2dust/sing-box-rules/rule-set-geoip/$f" || true
  done
  for f in \
    geosite-cn.srs geosite-gfw.srs geosite-greatfire.srs \
    geosite-geolocation-cn.srs geosite-category-ads-all.srs geosite-private.srs; do
    curl -fsSL -o "$srss_dir/$f" \
      "https://raw.githubusercontent.com/2dust/sing-box-rules/rule-set-geosite/$f" || true
  done

  # Unify to bin/
  unify_geo_layout "$outroot"
}

# Prefer the prebuilt v2rayN core bundle; then unify geo layout
download_v2rayn_bundle() {
  local outroot="$1"
  local url=""
  if [[ "$RID_DIR" == "linux-arm64" ]]; then
    url="https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-arm64.zip"
  else
    url="https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-64.zip"
  fi
  echo "[+] Try v2rayN bundle archive: $url"
  local tmp zipname
  tmp="$(mktemp -d)"; zipname="$tmp/v2rayn.zip"
  curl -fL "$url" -o "$zipname" || { echo "[!] Bundle download failed"; return 1; }
  unzip -q "$zipname" -d "$tmp" || { echo "[!] Bundle unzip failed"; return 1; }

  if [[ -d "$tmp/bin" ]]; then
    mkdir -p "$outroot/bin"
    rsync -a "$tmp/bin/" "$outroot/bin/"
  else
    rsync -a "$tmp/" "$outroot/"
  fi

  rm -f "$outroot/v2rayn.zip" 2>/dev/null || true
  # keep mihomo
  # find "$outroot" -type d -name "mihomo" -prune -exec rm -rf {} + 2>/dev/null || true

  local nested_dir
  nested_dir="$(find "$outroot" -maxdepth 1 -type d -name 'v2rayN-linux-*' | head -n1 || true)"
  if [[ -n "${nested_dir:-}" && -d "$nested_dir/bin" ]]; then
    mkdir -p "$outroot/bin"
    rsync -a "$nested_dir/bin/" "$outroot/bin/"
    rm -rf "$nested_dir"
  fi

  # Unify to bin/
  unify_geo_layout "$outroot"

  echo "[+] Bundle extracted to $outroot"
}

# ===== Build results collection for --arch all ========================================
BUILT_RPMS=()     # Will collect absolute paths of built RPMs
BUILT_ALL=0       # Flag to know if we should print the final summary

# ===== Build (single-arch) function ====================================================
build_for_arch() {
  # $1: target short arch: x64 | arm64
  local short="$1"
  local rid rpm_target archdir
  case "$short" in
    x64)   rid="linux-x64";   rpm_target="x86_64"; archdir="x86_64" ;;
    arm64) rid="linux-arm64"; rpm_target="aarch64"; archdir="aarch64" ;;
    *) echo "[ERROR] Unknown arch '$short' (use x64|arm64)"; return 1;;
  esac

  echo "[*] Building for target: $short  (RID=$rid, RPM --target $rpm_target)"

  # .NET publish (self-contained) for this RID
  dotnet clean "$PROJECT" -c Release
  rm -rf "$(dirname "$PROJECT")/bin/Release/net8.0" || true

  dotnet restore "$PROJECT"
  dotnet publish "$PROJECT" \
    -c Release -r "$rid" \
    -p:PublishSingleFile=false \
    -p:SelfContained=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

  # Per-arch variables (scoped)
  local RID_DIR="$rid"
  local PUBDIR
  PUBDIR="$(dirname "$PROJECT")/bin/Release/net8.0/${RID_DIR}/publish"
  [[ -d "$PUBDIR" ]]

  # Make RID_DIR visible to download helpers (they read this var)
  export RID_DIR

  # Per-arch working area
  local PKGROOT="v2rayN-publish"
  local WORKDIR
  WORKDIR="$(mktemp -d)"
  trap '[[ -n "${WORKDIR:-}" ]] && rm -rf "$WORKDIR"' RETURN

  # rpmbuild topdir selection
  local TOPDIR SPECDIR SOURCEDIR USE_TOPDIR_DEFINE
  if [[ "$ID" =~ ^(rhel|rocky|almalinux|centos)$ ]]; then
    rpmdev-setuptree
    TOPDIR="${HOME}/rpmbuild"
    SPECDIR="${TOPDIR}/SPECS"
    SOURCEDIR="${TOPDIR}/SOURCES"
    USE_TOPDIR_DEFINE=0
  else
    TOPDIR="${WORKDIR}/rpmbuild"
    SPECDIR="${TOPDIR}/SPECS}"
    SOURCEDIR="${TOPDIR}/SOURCES"
    mkdir -p "${SPECDIR}" "${SOURCEDIR}" "${TOPDIR}/BUILD" "${TOPDIR}/RPMS" "${TOPDIR}/SRPMS"
    USE_TOPDIR_DEFINE=1
  fi

  # Stage publish content
  mkdir -p "$WORKDIR/$PKGROOT"
  cp -a "$PUBDIR/." "$WORKDIR/$PKGROOT/"

  # Optional icon
  local ICON_CANDIDATE
  ICON_CANDIDATE="$(dirname "$PROJECT")/../v2rayN.Desktop/v2rayN.png"
  [[ -f "$ICON_CANDIDATE" ]] && cp "$ICON_CANDIDATE" "$WORKDIR/$PKGROOT/v2rayn.png" || true

  # Prepare bin structure
  mkdir -p "$WORKDIR/$PKGROOT/bin/xray" "$WORKDIR/$PKGROOT/bin/sing_box"

  # Bundle / cores per-arch
  if [[ "$FORCE_NETCORE" -eq 0 ]]; then
    if download_v2rayn_bundle "$WORKDIR/$PKGROOT"; then
      echo "[*] Using v2rayN bundle archive."
    else
      echo "[*] Bundle failed, fallback to separate core + rules."
      if [[ "$WITH_CORE" == "xray" || "$WITH_CORE" == "both" ]]; then
        download_xray "$WORKDIR/$PKGROOT/bin/xray" || echo "[!] xray download failed (skipped)"
      fi
      if [[ "$WITH_CORE" == "sing-box" || "$WITH_CORE" == "both" ]]; then
        download_singbox "$WORKDIR/$PKGROOT/bin/sing_box" || echo "[!] sing-box download failed (skipped)"
      fi
      download_geo_assets "$WORKDIR/$PKGROOT" || echo "[!] Geo rules download failed (skipped)"
    fi
  else
    echo "[*] --netcore specified: use separate core + rules."
    if [[ "$WITH_CORE" == "xray" || "$WITH_CORE" == "both" ]]; then
      download_xray "$WORKDIR/$PKGROOT/bin/xray" || echo "[!] xray download failed (skipped)"
    fi
    if [[ "$WITH_CORE" == "sing-box" || "$WITH_CORE" == "both" ]]; then
      download_singbox "$WORKDIR/$PKGROOT/bin/sing_box" || echo "[!] sing-box download failed (skipped)"
    fi
    download_geo_assets "$WORKDIR/$PKGROOT" || echo "[!] Geo rules download failed (skipped)"
    # ---- REQUIRED: always fetch mihomo in netcore mode, per-arch ----
    download_mihomo "$WORKDIR/$PKGROOT" || echo "[!] mihomo download failed (skipped)"
  fi

  # Tarball
  mkdir -p "$SOURCEDIR"
  tar -C "$WORKDIR" -czf "$SOURCEDIR/$PKGROOT.tar.gz" "$PKGROOT"

  # SPEC
  local SPECFILE="$SPECDIR/v2rayN.spec"
  mkdir -p "$SPECDIR"
  cat > "$SPECFILE" <<'SPEC'
%global debug_package %{nil}
%undefine _debuginfo_subpackages
%undefine _debugsource_packages
# Ignore outdated LTTng dependencies incorrectly reported by the .NET runtime (to avoid installation failures)
%global __requires_exclude ^liblttng-ust\.so\..*$

Name:           v2rayN
Version:        __VERSION__
Release:        1%{?dist}
Summary:        v2rayN (Avalonia) GUI client for Linux (x86_64/aarch64)
License:        GPL-3.0-only
URL:            https://github.com/2dust/v2rayN
BugURL:         https://github.com/2dust/v2rayN/issues
ExclusiveArch:  aarch64 x86_64
Source0:        __PKGROOT__.tar.gz

# Runtime dependencies (Avalonia / X11 / Fonts / GL)
Requires:       freetype, cairo, pango, openssl, mesa-libEGL, mesa-libGL
Requires:       glibc >= 2.34
Requires:       fontconfig >= 2.14
Requires:       desktop-file-utils >= 0.26
Requires:       xdg-utils >= 1.1.3
Requires:       coreutils >= 8.32

%description
v2rayN Linux for Red Hat Enterprise Linux
Support vless / vmess / Trojan / http / socks / Anytls / Hysteria2 / Shadowsocks / tuic / WireGuard
Support Red Hat Enterprise Linux / Fedora Linux / Rocky Linux / AlmaLinux / CentOS
For more information, Please visit our website
https://github.com/2dust/v2rayN

%prep
%setup -q -n __PKGROOT__

%build
# no build

%install
install -dm0755 %{buildroot}/opt/v2rayN
cp -a * %{buildroot}/opt/v2rayN/

# Launcher (prefer native ELF first, then DLL fallback)
install -dm0755 %{buildroot}%{_bindir}
cat > %{buildroot}%{_bindir}/v2rayn << 'EOF'
#!/usr/bin/bash
set -euo pipefail
DIR="/opt/v2rayN"

# Prefer native apphost
if [[ -x "$DIR/v2rayN" ]]; then exec "$DIR/v2rayN" "$@"; fi

# DLL fallback
for dll in v2rayN.Desktop.dll v2rayN.dll; do
  if [[ -f "$DIR/$dll" ]]; then exec /usr/bin/dotnet "$DIR/$dll" "$@"; fi
done

echo "v2rayN launcher: no executable found in $DIR" >&2
ls -l "$DIR" >&2 || true
exit 1
EOF
chmod 0755 %{buildroot}%{_bindir}/v2rayn

# Desktop file
install -dm0755 %{buildroot}%{_datadir}/applications
cat > %{buildroot}%{_datadir}/applications/v2rayn.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=v2rayN
Comment=v2rayN for Red Hat Enterprise Linux
Exec=v2rayn
Icon=v2rayn
Terminal=false
Categories=Network;
EOF

# Icon
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

  # Autostart injection (inside %install) and %files entry
  if [[ "$AUTOSTART" -eq 1 ]]; then
    awk '
      BEGIN{ins=0}
      /^%post$/ && !ins {
        print "# --- Autostart (.desktop) ---"
        print "install -dm0755 %{buildroot}/etc/xdg/autostart"
        print "cat > %{buildroot}/etc/xdg/autostart/v2rayn.desktop << '\''EOF'\''"
        print "[Desktop Entry]"
        print "Type=Application"
        print "Name=v2rayN (Autostart)"
        print "Exec=v2rayn"
        print "X-GNOME-Autostart-enabled=true"
        print "NoDisplay=false"
        print "EOF"
        ins=1
      }
      {print}
    ' "$SPECFILE" > "${SPECFILE}.tmp" && mv "${SPECFILE}.tmp" "$SPECFILE"

    awk '
      BEGIN{infiles=0; done=0}
      /^%files$/        {infiles=1}
      infiles && done==0 && $0 ~ /%{_datadir}\/icons\/hicolor\/256x256\/apps\/v2rayn\.png/ {
        print
        print "%config(noreplace) /etc/xdg/autostart/v2rayn.desktop"
        done=1
        next
      }
      {print}
    ' "$SPECFILE" > "${SPECFILE}.tmp" && mv "${SPECFILE}.tmp" "$SPECFILE"
  fi

  # Replace placeholders
  sed -i "s/__VERSION__/${VERSION}/g" "$SPECFILE"
  sed -i "s/__PKGROOT__/${PKGROOT}/g" "$SPECFILE"

  # ----- Select proper 'strip' per target arch on Ubuntu only (cross-binutils) -----
  # NOTE: We define only __strip to point to the target-arch strip.
  #       DO NOT override __brp_strip (it must stay the brp script path).
  local STRIP_ARGS=()
  if [[ "$ID" == "ubuntu" ]]; then
    local STRIP_BIN=""
    if [[ "$short" == "x64" ]]; then
      STRIP_BIN="/usr/bin/x86_64-linux-gnu-strip"
    else
      STRIP_BIN="/usr/bin/aarch64-linux-gnu-strip"
    fi
    if [[ -x "$STRIP_BIN" ]]; then
      STRIP_ARGS=( --define "__strip $STRIP_BIN" )
    fi
  fi

  # Build RPM for this arch (force rpm --target to match compile arch)
  if [[ "$USE_TOPDIR_DEFINE" -eq 1 ]]; then
    rpmbuild -ba "$SPECFILE" --define "_topdir $TOPDIR" --target "$rpm_target" "${STRIP_ARGS[@]}"
  else
    rpmbuild -ba "$SPECFILE" --target "$rpm_target" "${STRIP_ARGS[@]}"
  fi

  # Copy temporary rpmbuild to ~/rpmbuild on Debian/Ubuntu path
  if [[ "$USE_TOPDIR_DEFINE" -eq 1 ]]; then
    mkdir -p "$HOME/rpmbuild"
    rsync -a "$TOPDIR"/ "$HOME/rpmbuild"/
    TOPDIR="$HOME/rpmbuild"
  fi

  echo "Build done for $short. RPM at:"
  local f
  for f in "${TOPDIR}/RPMS/${archdir}/v2rayN-${VERSION}-1"*.rpm; do
    [[ -e "$f" ]] || continue
    echo "  $f"
    BUILT_RPMS+=("$f")
  done
}

# ===== Arch selection and build orchestration =========================================
case "${ARCH_OVERRIDE:-}" in
  "")
    # No --arch: use host architecture
    if [[ "$host_arch" == "aarch64" ]]; then
      build_for_arch arm64
    else
      build_for_arch x64
    fi
    ;;
  x64|amd64)
    build_for_arch x64
    ;;
  arm64|aarch64)
    build_for_arch arm64
    ;;
  all)
    BUILT_ALL=1
    # Build x64 and arm64 separately; each package contains its own arch-only binaries.
    build_for_arch x64
    build_for_arch arm64
    ;;
  *)
    echo "[ERROR] Unknown --arch '${ARCH_OVERRIDE}'. Use x64|arm64|all."
    exit 1
    ;;
esac

# ===== Final summary if building both arches ==========================================
if [[ "$BUILT_ALL" -eq 1 ]]; then
  echo ""
  echo "================ Build Summary (both architectures) ================"
  if [[ "${#BUILT_RPMS[@]}" -gt 0 ]]; then
    for rp in "${BUILT_RPMS[@]}"; do
      echo "$rp"
    done
  else
    echo "[WARN] No RPMs detected in summary (check build logs above)."
  fi
  echo "==================================================================="
fi
