#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo "Usage: $0 <linux-x64|linux-arm64|linux-riscv64|linux-loongarch64> <publish-directory> [dist-directory]" >&2
  exit 2
fi

rid="$1"
publish_dir="$2"
dist_dir="${3:-dist}"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

# Asset names and ZIP boundaries are owned by web-release-assets.py so the packager, the
# manifest tool, and the runtime updater share ../Assets/web-assets.json.
python3 "$script_dir/web-release-assets.py" package \
  --rid "$rid" \
  --publish "$publish_dir" \
  --dist "$dist_dir"
