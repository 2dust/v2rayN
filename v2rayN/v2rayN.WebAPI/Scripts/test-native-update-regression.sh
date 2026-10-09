#!/usr/bin/env bash
# Exercise real single-file updates with deliberately different bundle layouts.
set -euo pipefail

web_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
previous="$(realpath "${1:?Usage: test-native-update-regression.sh <native-publish-directory>}")"
test -x "$previous/v2rayN.WebAPI"
test -s "$previous/v2rayN.WebAPI.build.json"

dotnet="${DOTNET:-$web_root/.NET/dotnet}"
if [[ "$dotnet" != */* ]]; then
  dotnet="$(command -v "$dotnet")"
elif [[ ! -x "$dotnet" ]]; then
  dotnet="$(command -v dotnet)"
fi
export NUGET_PACKAGES="${NUGET_PACKAGES:-$web_root/.packages/nuget}"

# Keep native bundles and test installs beside the publish directory, not on /tmp tmpfs.
scratch="$(mktemp -d "$(dirname -- "$previous")/.webapi-update-regression.XXXXXX")"
cleanup() {
  local status=$?
  if [[ "$status" -eq 0 ]]; then
    rm -rf -- "$scratch"
  else
    printf 'Native update regression fixture retained: %s\n' "$scratch" >&2
  fi
}
trap cleanup EXIT

mapfile -t identity < <(python3 - "$previous/v2rayN.WebAPI.build.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as source:
    identity = json.load(source)
assert identity["product"] == "v2rayN.WebAPI"
print("0.0.1-regression" if identity["version"] != "0.0.1-regression" else "0.0.2-regression")
print(identity["commit"])
print(identity["buildDate"])
print(identity["rid"])
PY
)
[[ "${#identity[@]}" -eq 4 ]]

# A test-only assembly attribute grows the managed payload and shifts bundle offsets.
# Version-only fixtures can retain identical offsets and hide lazy-loading failures.
python3 - "$scratch/layout.targets" <<'PY'
from pathlib import Path
import sys

Path(sys.argv[1]).write_text(
    '<Project><ItemGroup Condition="\'$(MSBuildProjectName)\' == \'v2rayN.WebAPI\'">'
    '<AssemblyMetadata Include="NativeUpdateRegressionPadding" Value="' + "x" * 8192 +
    '" /></ItemGroup></Project>', encoding="utf-8")
PY

"$dotnet" publish "$web_root/v2rayN.WebAPI.csproj" \
  --configuration Release --runtime "${identity[3]}" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:WebVersion="${identity[0]}" -p:WebCommit="${identity[1]}" -p:WebBuildDate="${identity[2]}" \
  -p:WebRepository="${V2RAYN_WEB_REPOSITORY:-${GITHUB_REPOSITORY:-2dust/v2rayN}}" \
  -p:CustomBeforeMicrosoftCommonTargets="$scratch/layout.targets" \
  --output "$scratch/candidate"

python3 - "$previous" "$scratch/candidate" "${identity[0]}" <<'PY'
from pathlib import Path
import json
import sys

previous, candidate = map(Path, sys.argv[1:3])
identity = json.loads((previous / "v2rayN.WebAPI.build.json").read_text())
identity["version"] = sys.argv[3]
(candidate / "v2rayN.WebAPI.build.json").write_text(json.dumps(identity), encoding="utf-8")
assert (previous / "v2rayN.WebAPI").stat().st_size != (candidate / "v2rayN.WebAPI").stat().st_size, \
    "Regression fixtures must have different bundle layouts, not just different versions"
PY

identity_test=(python3 "$web_root/Scripts/test-native-update-identity.py" "$previous" "$scratch/candidate")
# Run in a transient scope so a hosted runner's enclosing systemd service is not mistaken for
# the WebAPI service under test. Production still rejects actual systemd-managed WebAPI updates.
if command -v systemd-run >/dev/null && systemd-run --user --scope --quiet /usr/bin/true >/dev/null 2>&1; then
  systemd-run --user --scope --quiet /usr/bin/env TMPDIR="$scratch" "${identity_test[@]}"
elif command -v sudo >/dev/null && sudo -n systemd-run --scope --quiet /usr/bin/true >/dev/null 2>&1; then
  runuser_bin="$(command -v runuser)"
  sudo -n systemd-run --scope --quiet "$runuser_bin" --preserve-environment -u "$(id -un)" -- \
    /usr/bin/env TMPDIR="$scratch" "${identity_test[@]}"
elif grep -Eq 'system\.slice/.*\.service' /proc/self/cgroup; then
  echo 'Native update regression is in a systemd service cgroup, but no transient scope is available.' >&2
  exit 1
else
  TMPDIR="$scratch" "${identity_test[@]}"
fi
printf 'Native single-file different-layout update regression passed.\n'
