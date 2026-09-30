#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
  echo "Usage: $0 <official Xray Linux release .zip>" >&2
  exit 2
fi

web_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
target="$web_root/core-bin/xray"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

unzip -q "$1" -d "$stage"
binary="$(find "$stage" -type f -name xray -print -quit)"
if [[ -z "$binary" ]]; then
  echo "Archive does not contain an Xray executable." >&2
  exit 1
fi

chmod 755 "$binary"
version_output="$("$binary" -version 2>&1)"
if ! grep -Eiq 'Xray.*[0-9]+\.[0-9]+\.[0-9]+' <<<"$version_output"; then
  echo "The extracted executable failed its Xray version check." >&2
  exit 1
fi

mkdir -p "$target"
install -m 755 "$binary" "$target/.xray.new"
"$target/.xray.new" -version >/dev/null
mv -f "$target/.xray.new" "$target/xray"
for data_file in geoip.dat geosite.dat; do
  source_file="$(find "$stage" -type f -name "$data_file" -print -quit)"
  if [[ -n "$source_file" ]]; then
    install -m 644 "$source_file" "$target/$data_file"
  fi
done

echo "Installed $("$target/xray" -version | head -n 1) at $target/xray"
