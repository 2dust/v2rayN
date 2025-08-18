#!/usr/bin/env bash
set -euo pipefail

# ===== Require Red Hat Enterprise Linux/RockyLinux/AlmaLinux/CentOS OR Ubuntu/Debian ====
if [[ -r /etc/os-release ]]; then
  . /etc/os-release
  case "$ID" in
    rhel|rocky|almalinux|centos|ubuntu|debian)
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

# If the first argument starts with --, don’t treat it as version number
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
    *)
      if [[ -z "${VERSION_ARG:-}" ]]; then VERSION_ARG="$1"; fi
      shift;;
  esac
done

# ===== Environment check + Dependencies ========================================
arch="$(uname -m)"
[[ "$arch" == "aarch64" || "$arch" == "x86_64" ]] || { echo "Only supports aarch64 / x86_64"; exit 1; }

install_ok=0
case "$ID" in
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
  ubuntu|debian)
    sudo apt-get update
    if [[ "${ID:-}" == "ubuntu" ]]; then
      if ! apt-cache policy | grep -q '^500 .*ubuntu.com/ubuntu.* universe'; then
        sudo apt-get -y install software-properties-common || true
        sudo add-apt-repository -y universe || true
        sudo apt-get update
      fi
    fi
    sudo apt-get -y install curl unzip tar rsync rpm || true
    if ! command -v rpmbuild >/dev/null 2>&1; then
      echo "[ERROR] 'rpmbuild' not found after installing 'rpm'."
      echo "        Please ensure the 'rpm' package is available from your repos (universe on Ubuntu)."
      exit 1
    fi
    if ! command -v dotnet >/dev/null 2>&1; then
      sudo apt-get -y install dotnet-sdk-8.0 || true
      sudo apt-get -y install dotnet-sdk-8 || true
      sudo apt-get -y install dotnet-sdk || true
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

# Git submodules (tolerant)
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
  curl -fsSL "https://api.github.com/repos/2dust/v2rayN/releases/latest" \
    | grep -Eo '"tag_name":\s*"v?[^"]+"' \
    | head -n1 \
    | sed -E 's/.*"tag_name":\s*"v?([^"]+)".*/\1/'
}

get_latest_tag_prerelease() {
  local json
  json="$(curl -fsSL "https://api.github.com/repos/2dust/v2rayN/releases?per_page=20")" || return 1
  echo "$json" \
    | awk -v RS='},' '/"prerelease":[[:space:]]*true/ { if (match($0, /"tag_name":[[:space:]]*"v?[^"]+"/, m)) { t=m[0]; sub(/.*"tag_name":[[:space:]]*"?v?/, "", t); sub(/".*/, "", t); print t; exit } }'
}

git_try_checkout() {
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

# ===== .NET publish ===========================================================
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

# ===== Helpers for core/rules download =======================================
download_xray() {
  local outdir="$1" ver="${XRAY_VER:-}" url tmp zipname="xray.zip"
  mkdir -p "$outdir"
  if [[ -z "$ver" ]]; then
    ver="$(curl -fsSL https://api.github.com/repos/XTLS/Xray-core/releases/latest \
        | grep -Eo '"tag_name":\s*"v[^"]+"' | sed -E 's/.*"v([^"]+)".*/\1/' | head -n1)" || true
  fi
  [[ -n "$ver" ]] || { echo "[xray] Failed to get version"; return 1; }
  if [[ "$arch" == "aarch64" ]]; then
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-arm64-v8a.zip"
  else
    url="https://github.com/XTLS/Xray-core/releases/download/v${ver}/Xray-linux-64.zip"
  fi
  echo "[+] Download xray: $url"
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
  [[ -n "$ver" ]] || { echo "[sing-box] Failed to get version"; return 1; }
  if [[ "$arch" == "aarch64" ]]; then
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-arm64.tar.gz"
  else
    url="https://github.com/SagerNet/sing-box/releases/download/v${ver}/sing-box-${ver}-linux-amd64.tar.gz"
  fi
  echo "[+] Download sing-box: $url"
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
  curl -fL "$url" -o "$tmp/$tarname"
  tar -C "$tmp" -xzf "$tmp/$tarname"
  bin="$(find "$tmp" -type f -name 'sing-box' | head -n1 || true)"
  [[ -n "$bin" ]] || { echo "[!] sing-box unpack failed"; return 1; }
  install -Dm755 "$bin" "$outdir/sing-box"
}

# 统一 geo 文件布局到 bin/xray/
unify_geo_layout() {
  local outroot="$1"
  mkdir -p "$outroot/bin/xray"
  local srcs=( \
    "$outroot/bin/geosite.dat" \
    "$outroot/bin/geoip.dat" \
    "$outroot/bin/geoip-only-cn-private.dat" \
    "$outroot/bin/Country.mmdb" \
    "$outroot/bin/geoip.metadb" \
  )
  for s in "${srcs[@]}"; do
    if [[ -f "$s" ]]; then
      mv -f "$s" "$outroot/bin/xray/$(basename "$s")"
    fi
  done
}

# Geo 规则（先按 ZIP-like 下载到 bin/，随后统一到 bin/xray/）
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

  # 关键：统一到 bin/xray/
  unify_geo_layout "$outroot"
}

# 优先使用 v2rayN 打包好的 bundle
download_v2rayn_bundle() {
  local outroot="$1"
  local url=""
  if [[ "$arch" == "aarch64" ]]; then
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
  find "$outroot" -type d -name "mihomo" -prune -exec rm -rf {} + 2>/dev/null || true

  local nested_dir
  nested_dir="$(find "$outroot" -maxdepth 1 -type d -name 'v2rayN-linux-*' | head -n1 || true)"
  if [[ -n "${nested_dir:-}" && -d "$nested_dir/bin" ]]; then
    mkdir -p "$outroot/bin"
    rsync -a "$nested_dir/bin/" "$outroot/bin/"
    rm -rf "$nested_dir"
  fi

  # 关键：统一到 bin/xray/
  unify_geo_layout "$outroot"

  echo "[+] Bundle extracted to $outroot"
}

# ===== Copy publish files to RPM build root ====================================
PKGROOT="v2rayN-publish"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

if [[ "$ID" =~ ^(rhel|rocky|almalinux|centos)$ ]]; then
  # --- RHEL path (unchanged) ---
  rpmdev-setuptree
  TOPDIR="${HOME}/rpmbuild"
  SPECDIR="${TOPDIR}/SPECS"
  SOURCEDIR="${TOPDIR}/SOURCES"
  USE_TOPDIR_DEFINE=0
else
  # --- Ubuntu/Debian path (temporary _topdir) ---
  TOPDIR="${WORKDIR}/rpmbuild"
  SPECDIR="${TOPDIR}/SPECS"
  SOURCEDIR="${TOPDIR}/SOURCES"
  mkdir -p "${SPECDIR}" "${SOURCEDIR}" "${TOPDIR}/BUILD" "${TOPDIR}/RPMS" "${TOPDIR}/SRPMS"
  USE_TOPDIR_DEFINE=1
fi

mkdir -p "$WORKDIR/$PKGROOT"
cp -a "$PUBDIR/." "$WORKDIR/$PKGROOT/"

# icon (Optional)
ICON_CANDIDATE="$(dirname "$PROJECT")/../v2rayN.Desktop/v2rayN.png"
[[ -f "$ICON_CANDIDATE" ]] && cp "$ICON_CANDIDATE" "$WORKDIR/$PKGROOT/v2rayn.png" || true

# bin directory structure
mkdir -p "$WORKDIR/$PKGROOT/bin/xray" "$WORKDIR/$PKGROOT/bin/sing_box"

# ====== Prefer bundle zip unless --netcore, else fall back =====================
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
fi

tar -C "$WORKDIR" -czf "$SOURCEDIR/$PKGROOT.tar.gz" "$PKGROOT"

# ===== Generate SPEC (heredoc with placeholders) ===================================
SPECFILE="$SPECDIR/v2rayN.spec"
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
ExclusiveArch:  aarch64 x86_64
Source0:        __PKGROOT__.tar.gz

# Runtime dependencies (Avalonia / X11 / Fonts / GL)
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

# Launcher (prioritize ELF first, then fall back to DLL; also create Geo symlinks for the user)
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

# Prefer native ELF（apphost）
if [[ -x "$DIR/v2rayN" ]]; then exec "$DIR/v2rayN" "$@"; fi

# DLL fallback (for framework-dependent publish)
for dll in v2rayN.Desktop.dll v2rayN.dll; do
  if [[ -f "$DIR/$dll" ]]; then exec /usr/bin/dotnet "$DIR/$dll" "$@"; fi
done

echo "v2rayN launcher: no executable found in $DIR" >&2
ls -l "$DIR" >&2 || true
exit 1
EOF
chmod 0755 %{buildroot}%{_bindir}/v2rayn

# Desktop File
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

# icon
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

# Optional: system-wide autostart (append block, keep original logic unchanged)
if [[ "$AUTOSTART" -eq 1 ]]; then
cat >> "$SPECFILE" <<'SPEC'
# System-wide autostart entry
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

# Injecting version/package root placeholders
sed -i "s/__VERSION__/${VERSION}/g" "$SPECFILE"
sed -i "s/__PKGROOT__/${PKGROOT}/g" "$SPECFILE"

# ===== Build RPM ================================================================
if [[ "$USE_TOPDIR_DEFINE" -eq 1 ]]; then
  rpmbuild -ba "$SPECFILE" --define "_topdir $TOPDIR"
else
  rpmbuild -ba "$SPECFILE"
fi

# ===== Ubuntu/Debian: move temporary rpmbuild to ~/rpmbuild ====================
if [[ "$USE_TOPDIR_DEFINE" -eq 1 ]]; then
  mkdir -p "$HOME/rpmbuild"
  rsync -a "$TOPDIR"/ "$HOME/rpmbuild"/
  TOPDIR="$HOME/rpmbuild"
fi

echo "Build done. RPM at:"
archdir="$( [[ "$arch" == "aarch64" ]] && echo aarch64 || echo x86_64 )"
ls -1 "${TOPDIR}/RPMS/${archdir}/v2rayN-${VERSION}-1"*.rpm
