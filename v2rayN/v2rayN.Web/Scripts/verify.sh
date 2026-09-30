#!/usr/bin/env bash
set -euo pipefail

web_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
upstream_root="$(dirname -- "$web_root")"
output_dir="${1:-$web_root/publish/verify-linux-x64}"

dotnet="${DOTNET:-}"
if [[ "$dotnet" != */* && -n "$dotnet" ]]; then
  dotnet="$(command -v "$dotnet" || true)"
fi
if [[ -z "$dotnet" ]]; then
  if [[ -x "$web_root/.NET/dotnet" ]]; then
    dotnet="$web_root/.NET/dotnet"
  else
    dotnet="$(command -v dotnet || true)"
  fi
fi
if [[ -z "$dotnet" || ! -x "$dotnet" ]]; then
  printf 'Required build tool is not available: dotnet (expected .NET/dotnet or PATH)\n' >&2
  exit 1
fi
export NUGET_PACKAGES="${NUGET_PACKAGES:-$web_root/.packages/nuget}"

"$dotnet" restore "$web_root/v2rayN.Web.csproj"
"$dotnet" restore "$web_root/Tests/v2rayN.Web.Tests.csproj"
"$dotnet" restore "$upstream_root/ServiceLib.Tests/ServiceLib.Tests.csproj"
"$dotnet" build "$web_root/v2rayN.Web.csproj" --configuration Release --no-restore
"$dotnet" test --project "$web_root/Tests/v2rayN.Web.Tests.csproj" --configuration Release --no-restore
"$dotnet" test --project "$upstream_root/ServiceLib.Tests/ServiceLib.Tests.csproj" --configuration Release --no-restore
bash "$web_root/Scripts/publish-native.sh" linux-x64 "$output_dir"
test -x "$output_dir/v2rayN.Web"
bash "$web_root/Scripts/test-startup-security.sh" "$output_dir/v2rayN.Web"
bash "$web_root/Scripts/test-release-packaging.sh"
smoke_dir="$(mktemp -d "${TMPDIR:-/tmp}/v2rayn-api-package-smoke.XXXXXX")"
trap 'rm -rf "$smoke_dir"' EXIT HUP INT TERM
mkdir -p "$smoke_dir/package/webui" "$smoke_dir/package/bin"
cp "$output_dir/v2rayN.Web" "$output_dir/v2rayN.Web.build.json" \
  "$web_root/.env.example" "$smoke_dir/package/"
cp "$web_root/Deploy/WebUI/README.txt" "$smoke_dir/package/webui/README.txt"
touch "$smoke_dir/package/bin/.keep"
python3 - "$smoke_dir/package" "$smoke_dir/v2rayN-api-smoke.zip" <<'PY'
from pathlib import Path
import sys
import zipfile

root, archive_path = map(Path, sys.argv[1:])
with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(root.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(root).as_posix())
PY
bash "$web_root/Scripts/test-full-zip-runtime.sh" "$smoke_dir/v2rayN-api-smoke.zip"
printf 'Web verification passed; native publish ready: %s\n' "$output_dir/v2rayN.Web"
