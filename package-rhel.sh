#!/usr/bin/env bash
set -euo pipefail

# ===== Require Red Hat Enterprise Linux / Rocky Linux / AlmaLinux / CentOS Stream =======
if [[ -r /etc/os-release ]]; then
  . /etc/os-release
  case "$ID" in
    rhel|rocky|almalinux|centos)
      echo "[OK] Detected supported system: $NAME $VERSION_ID"
      ;;
    *)
      echo "[ERROR] Unsupported system: $NAME ($ID)."
      echo "This script only supports Red Hat Enterprise Linux / Rocky Linux / AlmaLinux / CentOS Stream."
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
    --xray-ver)      XRAY_VER="${2:-}"; shift 2;;        # Specify xray version (optional)
    --singbox-ver)   SING_VER="${2:-}"; shift 2;;        # Specify sing-box version (optional)
    *)
      if [[ -z "${VERSION_ARG:-}" ]]; then VERSION_ARG="$1"; fi
      shift;;
  esac
done

# ===== Environment check ===============================================================
arch="$(uname -m)"
[[ "$arch" == "aarch64" || "$arch" == "x86_64" ]] || { echo "Only supports aarch64 / x86_64"; exit 1; }

# Dependencies (packaging shouldn’t be run as root, but this line needs sudo)
sudo dnf -y install dotnet-sdk-8.0 rpm-build rpmdevtools curl unzip tar || sudo dnf -y install dotnet-sdk
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

# Version
VERSION="${VERSION_ARG:-}"
if [[ -z "$VERSION" ]]; then
  if git describe --tags --abbrev=0 >/dev/null 2>&1; then
    VERSION="$(git describe --tags --abbrev=0)"
  else
    VERSION="0.0.0+git"
  fi
fi
VERSION="${VERSION#v}"   # Remove the prefix "v"

# ===== .NET publish (non-single file, self-contained) ===========================================
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

# ===== Download Core（Optional） ========================================================
download_xray() {
  local outdir="$1" ver="${XRAY_VER:-}" url tmp zipname="xray.zip"
  mkdir -p "$outdir"
  if [[ -z "$ver" ]]; then
    # Latest version
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

# === Geo rule download (new logic) ===================================
download_geo_assets() {
  local outroot="$1"
  local xray_dir="$outroot/bin/xray"
  local sbox_dir="$outroot/bin/sing_box"
  mkdir -p "$xray_dir" "$sbox_dir/rule-sets"

  echo "[+] Download Xray Geo（geosite/geoip/...）"
  curl -fsSL -o "$xray_dir/geosite.dat" \
    "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geosite.dat"
  curl -fsSL -o "$xray_dir/geoip.dat" \
    "https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geoip.dat"
  curl -fsSL -o "$xray_dir/geoip-only-cn-private.dat" \
    "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/geoip-only-cn-private.dat"
  curl -fsSL -o "$xray_dir/Country.mmdb" \
    "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb"

  echo "[+] Download sing-box rules & DB"
  # database (optional meta rules)
  curl -fsSL -o "$sbox_dir/geoip.metadb" \
    "https://github.com/MetaCubeX/meta-rules-dat/releases/latest/download/geoip.metadb" || true

  # Official 2dust srs rule-sets (common subsets)
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

# ===== Copy publish files to RPM build root ==================================================
rpmdev-setuptree
TOPDIR="${HOME}/rpmbuild"
SPECDIR="${TOPDIR}/SPECS"
SOURCEDIR="${TOPDIR}/SOURCES"

PKGROOT="v2rayN-publish"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

mkdir -p "$WORKDIR/$PKGROOT"
cp -a "$PUBDIR/." "$WORKDIR/$PKGROOT/"

# icon（Optional）
ICON_CANDIDATE="$(dirname "$PROJECT")/../v2rayN.Desktop/v2rayN.png"
[[ -f "$ICON_CANDIDATE" ]] && cp "$ICON_CANDIDATE" "$WORKDIR/$PKGROOT/v2rayn.png" || true

# bin directory structure
mkdir -p "$WORKDIR/$PKGROOT/bin/xray" "$WORKDIR/$PKGROOT/bin/sing_box"

# Core
if [[ "$WITH_CORE" == "xray" || "$WITH_CORE" == "both" ]]; then
  download_xray "$WORKDIR/$PKGROOT/bin/xray" || echo "[!] xray download failed (skipped)）"
fi
if [[ "$WITH_CORE" == "sing-box" || "$WITH_CORE" == "both" ]]; then
  download_singbox "$WORKDIR/$PKGROOT/bin/sing_box" || echo "[!] sing-box download failed (skipped)"
fi

# Geo / rule-sets
download_geo_assets "$WORKDIR/$PKGROOT" || echo "[!] Geo rules download failed (skipped)"

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
rpmbuild -ba "$SPECFILE"

echo "Build done. RPM at:"
ls -1 "${TOPDIR}/RPMS/$( [[ "$arch" == "aarch64" ]] && echo aarch64 || echo x86_64 )/v2rayN-${VERSION}-1"*.rpm
