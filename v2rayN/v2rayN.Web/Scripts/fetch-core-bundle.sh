#!/bin/sh
set -eu

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <linux-x64|linux-arm64> <destination-bin-directory>" >&2
  exit 2
fi

rid="$1"
destination="$2"

case "$rid" in
  linux-x64) bundle="v2rayN-linux-64" ;;
  linux-arm64) bundle="v2rayN-linux-arm64" ;;
  *)
    echo "Unsupported runtime identifier: $rid" >&2
    exit 2
    ;;
esac

temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT HUP INT TERM

bundle_url="https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/$bundle.zip"
curl --fail --location --retry 3 --retry-delay 2 --silent --show-error \
  --output "$temporary_directory/core-bundle.zip" "$bundle_url"
unzip -q "$temporary_directory/core-bundle.zip" -d "$temporary_directory/unpacked"

source_bin="$temporary_directory/unpacked/$bundle/bin"
# These files are sanity checks for the current upstream payload, never an allowlist.
for required_file in \
  xray/xray \
  sing_box/sing-box \
  sing_box/libcronet.so \
  mihomo/mihomo \
  Country.mmdb \
  geoip-only-cn-private.dat \
  geoip.dat \
  geoip.metadb \
  geosite.dat \
  srss/geoip-cn.srs \
  srss/geosite-cn.srs \
  srss/geosite-category-ads-all.srs; do
  if [ ! -f "$source_bin/$required_file" ]; then
    echo "Core bundle is missing required asset: $required_file" >&2
    exit 1
  fi
done

mkdir -p "$destination"
# Preserve the complete official bin/ tree, including future upstream assets and Cores.
cp -a "$source_bin/." "$destination/"
chmod 755 \
  "$destination/xray/xray" \
  "$destination/sing_box/sing-box" \
  "$destination/mihomo/mihomo"
chmod 644 "$destination/sing_box/libcronet.so"

printf 'Integrated complete upstream %s Core/data bin payload into %s\n' "$rid" "$destination"
