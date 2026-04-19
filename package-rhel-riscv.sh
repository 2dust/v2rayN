#!/usr/bin/env bash
set -euo pipefail

# Require Red Hat base branch
. /etc/os-release

case "${ID:-}" in
  rhel|rocky|almalinux|fedora|centos)
    echo "Detected supported system: ${NAME:-$ID} ${VERSION_ID:-}"
    ;;
  *)
    echo "Unsupported system: ${NAME:-unknown} (${ID:-unknown})."
    echo "This script only supports: RHEL / Rocky / AlmaLinux / Fedora / CentOS."
    exit 1
    ;;
esac

# Kernel version
MIN_KERNEL="5.10"
CURRENT_KERNEL="$(uname -r)"

lowest="$(printf '%s\n%s\n' "$MIN_KERNEL" "$CURRENT_KERNEL" | sort -V | head -n1)"

if [[ "$lowest" != "$MIN_KERNEL" ]]; then
    echo "Kernel $CURRENT_KERNEL is below $MIN_KERNEL"
    exit 1
fi

echo "[OK] Kernel $CURRENT_KERNEL verified."

# Config & Parse arguments
VERSION_ARG="${1:-}"     # Pass version number like 7.13.8, or leave empty
WITH_CORE="both"         # Default: bundle both xray+sing-box
FORCE_NETCORE=0          # --netcore => skip archive bundle, use separate downloads
BUILD_FROM=""            # --buildfrom 1|2|3 to select channel non-interactively
DOTNET_RISCV_VERSION="10.0.105"
DOTNET_RISCV_BASE="https://github.com/filipnavara/dotnet-riscv/releases/download"
DOTNET_RISCV_FILE="dotnet-sdk-${DOTNET_RISCV_VERSION}-linux-riscv64.tar.gz"
DOTNET_SDK_URL="${DOTNET_RISCV_BASE}/${DOTNET_RISCV_VERSION}/${DOTNET_RISCV_FILE}"
SKIA_VER="${SKIA_VER:-3.119.2}"
HARFBUZZ_VER="${HARFBUZZ_VER:-8.3.1.1}"

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
    --xray-ver)      XRAY_VER="${2:-}"; shift 2;;
    --singbox-ver)   SING_VER="${2:-}"; shift 2;;
    --netcore)       FORCE_NETCORE=1; shift;;
    --buildfrom)     BUILD_FROM="${2:-}"; shift 2;;
    *)
      if [[ -z "${VERSION_ARG:-}" ]]; then VERSION_ARG="$1"; fi
      shift;;
  esac
done

# Conflict: version number AND --buildfrom cannot be used together
if [[ -n "${VERSION_ARG:-}" && -n "${BUILD_FROM:-}" ]]; then
  echo "You cannot specify both an explicit version and --buildfrom at the same time."
  echo "        Provide either a version (e.g. 7.14.0) OR --buildfrom 1|2|3."
  exit 1
fi

apply_riscv_patch() {
  # Ensure all project files target net10.0
  find . -type f \( -name "*.csproj" -o -name "*.props" -o -name "*.targets" \) \
    -exec sed -i 's/net8\.0/net10.0/g' {} +

  # Patch all Directory.Packages.props for SkiaSharp/HarfBuzzSharp
  while IFS= read -r -d '' f; do
    # replace existing versions if present
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

  # Patch SDK bundled RIDs
  f="$(find "$DOTNET_ROOT/sdk/$(dotnet --version)" -type f -name 'Microsoft.NETCoreSdk.BundledVersions.props' | head -n1 || true)"
  [[ -f "$f" ]] && sed -i \
    -e 's/linux-arm64/&;linux-riscv64/g' \
    -e 's/linux-musl-arm64/&;linux-musl-riscv64/g' \
    "$f"
}

build_sqlite_native_riscv64() {
  local outdir="$1"
  local workdir sqlite_year sqlite_ver sqlite_zip srcdir

  mkdir -p "$outdir"
  workdir="$(mktemp -d)"

  # SQLite 3.51.3 amalgamation
  sqlite_year="2026"
  sqlite_ver="3510300"
  sqlite_zip="sqlite-amalgamation-${sqlite_ver}.zip"

  echo "[+] Download SQLite amalgamation: ${sqlite_zip}"
  curl -fL "https://www.sqlite.org/${sqlite_year}/${sqlite_zip}" -o "${workdir}/${sqlite_zip}"

  unzip -q "${workdir}/${sqlite_zip}" -d "$workdir"
  srcdir="$(find "$workdir" -maxdepth 1 -type d -name 'sqlite-amalgamation-*' | head -n1 || true)"
  [[ -n "$srcdir" ]] || { echo "[!] SQLite source unpack failed"; rm -rf "$workdir"; return 1; }

  echo "[+] Build libe_sqlite3.so for riscv64"
  gcc -shared -fPIC -O2 \
    -DSQLITE_THREADSAFE=1 \
    -DSQLITE_ENABLE_FTS5 \
    -DSQLITE_ENABLE_RTREE \
    -DSQLITE_ENABLE_JSON1 \
    -o "${outdir}/libe_sqlite3.so" "${srcdir}/sqlite3.c" -ldl -lpthread

  rm -rf "$workdir"
}

copy_skiasharp_native_riscv64() {
  local outdir="$1"
  local skia_so=""
  local harfbuzz_so=""

  mkdir -p "$outdir"

  skia_so="$(find "$HOME/.nuget/packages" -path "*/skiasharp.nativeassets.linux/${SKIA_VER}/runtimes/linux-riscv64/native/libSkiaSharp.so" | head -n1 || true)"
  if [[ -z "$skia_so" ]]; then
    skia_so="$(find "$HOME/.nuget/packages" -path "*/runtimes/linux-riscv64/native/libSkiaSharp.so" | head -n1 || true)"
  fi

  harfbuzz_so="$(find "$HOME/.nuget/packages" -path "*/harfbuzzsharp.nativeassets.linux/${HARFBUZZ_VER}/runtimes/linux-riscv64/native/libHarfBuzzSharp.so" | head -n1 || true)"
  if [[ -z "$harfbuzz_so" ]]; then
    harfbuzz_so="$(find "$HOME/.nuget/packages" -path "*/runtimes/linux-riscv64/native/libHarfBuzzSharp.so" | head -n1 || true)"
  fi

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

# Check and install dependencies
host_arch="$(uname -m)"
[[ "$host_arch" == "riscv64" ]] || { echo "Only supports riscv64"; exit 1; }

install_ok=0

if command -v dnf >/dev/null 2>&1; then
  sudo dnf -y install \
    rpm-build rpmdevtools curl unzip tar jq rsync git python3 gcc make \
    glibc-devel kernel-headers libatomic file ca-certificates libicu\
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
  echo "Could not auto-install dependencies for '$ID'. Make sure these are available:"
  echo "dotnet-riscv SDK, curl, unzip, tar, rsync, git, python3, gcc, rpm, rpmdevtools, rpm-build (on Red Hat branch)"
  exit 1
fi

# Root directory
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Git submodules (best effort)
if [[ -f .gitmodules ]]; then
  git submodule sync --recursive || true
  git submodule update --init --recursive || true
fi

# Locate project
PROJECT="v2rayN.Desktop/v2rayN.Desktop.csproj"
if [[ ! -f "$PROJECT" ]]; then
  PROJECT="$(find . -maxdepth 3 -name 'v2rayN.Desktop.csproj' | head -n1 || true)"
fi
[[ -f "$PROJECT" ]] || { echo "v2rayN.Desktop.csproj not found"; exit 1; }

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

git_try_checkout() {
  # Try a series of refs and checkout when found.
  local want="$1" ref=""
  if git rev-parse --git-dir >/dev/null 2>&1; then
    git fetch --tags --force --prune --depth=1 || true
    if git rev-parse "refs/tags/${want}" >/dev/null 2>&1; then
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

apply_channel_or_keep() {
  local ch="$1" tag

  if [[ "$ch" == "keep" ]]; then
    echo "[*] Keep current repository state (no checkout)."
    VERSION="$(git describe --tags --abbrev=0 2>/dev/null || echo '0.0.0+git')"
    VERSION="${VERSION#v}"
    return 0
  fi

  echo "[*] Resolving ${ch} tag from GitHub releases..."
  if [[ "$ch" == "prerelease" ]]; then
    tag="$(get_latest_tag_prerelease || true)"
  else
    tag="$(get_latest_tag_latest || true)"
  fi

  [[ -n "$tag" ]] || { echo "Failed to resolve latest tag for channel '${ch}'."; exit 1; }
  echo "[*] Latest tag for '${ch}': ${tag}"
  git_try_checkout "$tag" || { echo "Failed to checkout '${tag}'."; exit 1; }
  VERSION="${tag#v}"
}

if git rev-parse --git-dir >/dev/null 2>&1; then
  if [[ -n "${VERSION_ARG:-}" ]]; then
    clean_ver="${VERSION_ARG#v}"
    if git_try_checkout "$clean_ver"; then
      VERSION="$clean_ver"
    else
      echo "[WARN] Tag '${VERSION_ARG}' not found."
      ch="$(choose_channel)"
      apply_channel_or_keep "$ch"
    fi
  else
    ch="$(choose_channel)"
    apply_channel_or_keep "$ch"
  fi
else
  echo "Current directory is not a git repo; proceeding on current tree."
  VERSION="${VERSION_ARG:-0.0.0}"
fi

VERSION="${VERSION#v}"
echo "[*] GUI version resolved as: ${VERSION}"

# riscv64 patch
apply_riscv_patch

# Helpers for core
download_xray() {
  # Download Xray core
  local outdir="$1" rid="$2" ver="${XRAY_VER:-}" url="" tmp zipname="xray.zip"
  mkdir -p "$outdir"
  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/XTLS/Xray-core/releases/latest \
        | grep -Eo '"tag_name":\s*"v[^"]+"' | sed -E 's/.*"v([^"]+)".*/\1/' | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[xray] Failed to get version"; return 1; }
  if [[ "$rid" == "linux-riscv64" ]]; then
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-riscv64.zip"
  fi
  [[ -n "$url" ]] || { echo "[xray] Unsupported RID: $rid"; return 1; }
  echo "[+] Download xray: $url"
  tmp="$(mktemp -d)"
  curl -fL "$url" -o "$tmp/$zipname"
  unzip -q "$tmp/$zipname" -d "$tmp"
  install -m 755 "$tmp/xray" "$outdir/xray"
  rm -rf "$tmp"
}

download_singbox() {
  # Download sing-box
  local outdir="$1" rid="$2" ver="${SING_VER:-}" url="" tmp tarname="singbox.tar.gz" bin cronet
  mkdir -p "$outdir"
  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/SagerNet/sing-box/releases/latest \
      | grep -Eo '"tag_name":\s*"v[^"]+"' \
      | sed -E 's/.*"v([^"]+)".*/\1/' \
      | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[sing-box] Failed to get version"; return 1; }
  if [[ "$rid" == "linux-riscv64" ]]; then
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-riscv64.tar.gz"
  fi
  [[ -n "$url" ]] || { echo "[sing-box] Unsupported RID: $rid"; return 1; }
  echo "[+] Download sing-box: $url"
  tmp="$(mktemp -d)"
  curl -fL "$url" -o "$tmp/$tarname"
  tar -C "$tmp" -xzf "$tmp/$tarname"
  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box unpack failed"; rm -rf "$tmp"; return 1; }
  install -m 755 "$bin" "$outdir/sing-box"
  cronet="$(find "$tmp" -type f -name 'libcronet*.so*' | head -n1 || true)"
  [[ -n "$cronet" ]] && install -m 644 "$cronet" "$outdir/libcronet.so"
  rm -rf "$tmp"
}

# Move geo files to outroot/bin
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
    if [[ -f "$outroot/bin/xray/$n" ]]; then
      mv -f "$outroot/bin/xray/$n" "$outroot/bin/$n"
    fi
  done
}

# Download geo/rule assets
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
    geosite-cn.srs geosite-gfw.srs geosite-google.srs geosite-greatfire.srs \
    geosite-geolocation-cn.srs geosite-category-ads-all.srs geosite-private.srs; do
    curl -fsSL -o "$srss_dir/$f" \
      "https://raw.githubusercontent.com/2dust/sing-box-rules/rule-set-geosite/$f" || true
  done

  # Unify to bin
  unify_geo_layout "$outroot"
}

# Prefer the prebuilt v2rayN core bundle; then unify geo layout
download_v2rayn_bundle() {
  local outroot="$1" rid="$2"
  local url=""
  if [[ "$rid" == "linux-riscv64" ]]; then
    url="https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-linux-riscv64.zip"
  fi
  [[ -n "$url" ]] || { echo "[!] Bundle unsupported RID: $rid"; return 1; }
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
  find "$outroot" -type d -name "mihomo" -prune -exec rm -rf {} + 2>/dev/null || true

  local nested_dir
  nested_dir="$(find "$outroot" -maxdepth 1 -type d -name 'v2rayN-linux-*' | head -n1 || true)"
  if [[ -n "$nested_dir" && -d "$nested_dir/bin" ]]; then
    mkdir -p "$outroot/bin"
    rsync -a "$nested_dir/bin/" "$outroot/bin/"
    rm -rf "$nested_dir"
  fi

  # Unify to bin/
  unify_geo_layout "$outroot"

  echo "[+] Bundle extracted to $outroot"
}

# ===== Build results collection ========================================================
BUILT_RPMS=()

# ===== Build (single-arch) function ====================================================
build_for_arch() {
  # $1: target short arch: riscv64
  local short="$1"
  local rid rpm_target archdir
  case "$short" in
    riscv64) rid="linux-riscv64"; rpm_target="riscv64"; archdir="riscv64" ;;
    *) echo "Unknown arch '$short' (use riscv64)"; return 1;;
  esac

  echo "[*] Building for target: $short  (RID=$rid, RPM --target $rpm_target)"

  # .NET publish (self-contained) for this RID
  dotnet clean "$PROJECT" -c Release -p:TargetFramework=net10.0
  rm -rf "$(dirname "$PROJECT")/bin/Release/net10.0" || true

  dotnet restore "$PROJECT" -r "$rid" -p:TargetFramework=net10.0
  dotnet publish "$PROJECT" \
    -c Release -r "$rid" \
    -p:TargetFramework=net10.0 \
    -p:PublishSingleFile=false \
    -p:SelfContained=true

  # Per-arch variables (scoped)
  local RID_DIR="$rid"
  local PUBDIR
  PUBDIR="$(dirname "$PROJECT")/bin/Release/net10.0/${RID_DIR}/publish"
  [[ -d "$PUBDIR" ]] || { echo "Publish directory not found: $PUBDIR"; return 1; }

  # Per-arch working area
  local PKGROOT="v2rayN-publish"
  local WORKDIR
  WORKDIR="$(mktemp -d)"
  trap '[[ -n "${WORKDIR:-}" ]] && rm -rf "$WORKDIR"' RETURN

  # rpmbuild topdir selection
  local TOPDIR SPECDIR SOURCEDIR PROJECT_DIR
  rpmdev-setuptree
  TOPDIR="${HOME}/rpmbuild"
  SPECDIR="${TOPDIR}/SPECS"
  SOURCEDIR="${TOPDIR}/SOURCES"

  # Stage publish content
  mkdir -p "$WORKDIR/$PKGROOT"
  cp -a "$PUBDIR/." "$WORKDIR/$PKGROOT/"

  copy_skiasharp_native_riscv64 "$WORKDIR/$PKGROOT" || echo "[!] SkiaSharp native copy failed (skipped)"
  build_sqlite_native_riscv64 "$WORKDIR/$PKGROOT" || echo "[!] sqlite native build failed (skipped)"

  # Required icon
  local ICON_CANDIDATE
  PROJECT_DIR="$(cd "$(dirname "$PROJECT")" && pwd)"
  ICON_CANDIDATE="$PROJECT_DIR/v2rayN.png"
  [[ -f "$ICON_CANDIDATE" ]] || { echo "Required icon not found: $ICON_CANDIDATE"; return 1; }
  cp "$ICON_CANDIDATE" "$WORKDIR/$PKGROOT/v2rayn.png"

  # Prepare bin structure
  mkdir -p "$WORKDIR/$PKGROOT/bin/xray" "$WORKDIR/$PKGROOT/bin/sing_box"

  # Bundle / cores per-arch
  fetch_separate_cores_and_rules() {
    local outroot="$1"

    if [[ "$WITH_CORE" == "xray" || "$WITH_CORE" == "both" ]]; then
      download_xray "$outroot/bin/xray" "$RID_DIR" || echo "[!] xray download failed (skipped)"
    fi
    if [[ "$WITH_CORE" == "sing-box" || "$WITH_CORE" == "both" ]]; then
      download_singbox "$outroot/bin/sing_box" "$RID_DIR" || echo "[!] sing-box download failed (skipped)"
    fi
    download_geo_assets "$outroot" || echo "[!] Geo rules download failed (skipped)"
  }

  if [[ "$FORCE_NETCORE" -eq 0 ]]; then
    if download_v2rayn_bundle "$WORKDIR/$PKGROOT" "$RID_DIR"; then
      echo "[*] Using v2rayN bundle archive."
    else
      echo "[*] Bundle failed, fallback to separate core + rules."
      fetch_separate_cores_and_rules "$WORKDIR/$PKGROOT"
    fi
  else
    echo "[*] --netcore specified: use separate core + rules."
    fetch_separate_cores_and_rules "$WORKDIR/$PKGROOT"
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
Summary:        v2rayN (Avalonia) GUI client for Linux (riscv64)
License:        GPL-3.0-only
URL:            https://github.com/2dust/v2rayN
BugURL:         https://github.com/2dust/v2rayN/issues
ExclusiveArch:  riscv64
Source0:        __PKGROOT__.tar.gz

# Runtime dependencies (Avalonia / X11 / Fonts / GL)
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
# no build

%install
install -dm0755 %{buildroot}/opt/v2rayN
cp -a * %{buildroot}/opt/v2rayN/

# Normalize permissions
find %{buildroot}/opt/v2rayN -type d -exec chmod 0755 {} +
find %{buildroot}/opt/v2rayN -type f -exec chmod 0644 {} +
[ -f %{buildroot}/opt/v2rayN/v2rayN ] && chmod 0755 %{buildroot}/opt/v2rayN/v2rayN || :
[ -f %{buildroot}/opt/v2rayN/libSkiaSharp.so ] && chmod 0755 %{buildroot}/opt/v2rayN/libSkiaSharp.so || :
[ -f %{buildroot}/opt/v2rayN/libHarfBuzzSharp.so ] && chmod 0755 %{buildroot}/opt/v2rayN/libHarfBuzzSharp.so || :
[ -f %{buildroot}/opt/v2rayN/libe_sqlite3.so ] && chmod 0755 %{buildroot}/opt/v2rayN/libe_sqlite3.so || :

# Launcher (prefer native ELF first, then DLL fallback)
install -dm0755 %{buildroot}%{_bindir}
install -m0755 /dev/stdin %{buildroot}%{_bindir}/v2rayn << 'EOF'
#!/usr/bin/bash
set -euo pipefail
DIR="/opt/v2rayN"
export LD_LIBRARY_PATH="$DIR:${LD_LIBRARY_PATH:-}"

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

# Desktop file
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

# Icon
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

  # Replace placeholders
  sed -i "s/__VERSION__/${VERSION}/g" "$SPECFILE"
  sed -i "s/__PKGROOT__/${PKGROOT}/g" "$SPECFILE"

  # Build RPM for this arch
  rpmbuild -ba "$SPECFILE" --target "$rpm_target"

  echo "Build done for $short. RPM at:"
  local f
  for f in "${TOPDIR}/RPMS/${archdir}/v2rayN-${VERSION}-1"*.rpm; do
    [[ -e "$f" ]] || continue
    echo "  $f"
    BUILT_RPMS+=("$f")
  done
}

# ===== Arch selection and build orchestration =========================================
targets=(riscv64)

for arch in "${targets[@]}"; do
  build_for_arch "$arch"
done

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
