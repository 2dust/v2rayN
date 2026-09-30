#!/usr/bin/env bash
set -euo pipefail

web_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
upstream_root="$(dirname -- "$web_root")"
repo_root="$(dirname -- "$upstream_root")"
rid="${1:-linux-x64}"
output_dir="${2:-$web_root/publish/$rid}"

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
case "$rid" in
  linux-x64|linux-arm64) ;;
  *) printf 'Supported runtime identifiers: linux-x64, linux-arm64 (received %s)\n' "$rid" >&2; exit 2 ;;
esac

export NUGET_PACKAGES="${NUGET_PACKAGES:-$web_root/.packages/nuget}"
# Official release builds pass the upstream release tag (for example 7.25.3) so the Web
# build identity follows the same version as the v2rayN Release. Local builds keep a dev identity.
web_version="${V2RAYN_WEB_VERSION:-0.0.0-dev}"
web_repository="${V2RAYN_WEB_REPOSITORY:-${GITHUB_REPOSITORY:-2dust/v2rayN}}"
web_commit="${V2RAYN_WEB_COMMIT:-$(git -C "$repo_root" rev-parse HEAD 2>/dev/null || printf unknown)}"
web_build_date="${V2RAYN_WEB_BUILD_DATE:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"
if [[ ! "$web_version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$ ]]; then
  printf 'Invalid Web version identity: %s\n' "$web_version" >&2
  exit 2
fi
if [[ ! "$web_repository" =~ ^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+$ ]]; then
  printf 'Invalid Web release repository: %s\n' "$web_repository" >&2
  exit 2
fi

"$dotnet" publish "$web_root/v2rayN.Web.csproj" \
  --configuration Release \
  --runtime "$rid" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:WebVersion="$web_version" \
  -p:WebCommit="$web_commit" \
  -p:WebBuildDate="$web_build_date" \
  -p:WebRepository="$web_repository" \
  --output "$output_dir"

python3 - "$output_dir/v2rayN.Web.build.json" "$web_version" "$web_commit" "$web_build_date" "$rid" <<'PY'
import json
import sys
from pathlib import Path

path, version, commit, build_date, rid = sys.argv[1:]
Path(path).write_text(json.dumps({
    "product": "v2rayN.Web",
    "version": version,
    "commit": commit,
    "buildDate": build_date,
    "rid": rid,
}, separators=(",", ":")) + "\n", encoding="utf-8")
PY
test -x "$output_dir/v2rayN.Web"
printf 'Native publish ready: %s (version %s, repository %s)\n' "$output_dir/v2rayN.Web" "$web_version" "$web_repository"
