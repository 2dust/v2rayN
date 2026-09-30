#!/usr/bin/env bash
# Release packaging dry-run: validates the full-install/app-only ZIP boundaries and the
# web-update.json manifest using small fixtures, without publishing anything.
set -euo pipefail

web_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
asset_tool="$web_root/Scripts/web-release-assets.py"
temporary="$(mktemp -d)"
trap 'rm -rf "$temporary"' EXIT HUP INT TERM

make_publish_fixture() {
  local rid="$1"
  local publish="$temporary/publish-$rid"
  mkdir -p "$publish/wwwroot/assets" "$publish/webui/assets" "$publish/bin/xray" "$publish/bin/sing_box" "$publish/bin/mihomo" "$publish/bin/srss"
  printf '#!/bin/sh\nexit 0\n' > "$publish/v2rayN.Web"
  chmod 755 "$publish/v2rayN.Web"
  printf '<html>stale bundled UI must not ship</html>\n' > "$publish/wwwroot/index.html"
  printf 'stale UI asset must not ship\n' > "$publish/wwwroot/assets/app.js"
  printf '<html>third-party UI must not ship</html>\n' > "$publish/webui/index.html"
  printf 'third-party UI asset must not ship\n' > "$publish/webui/assets/app.js"
  # Simulate accidental local environment files in the publish tree. Only the reviewed
  # repository template may be copied into a full install ZIP.
  printf 'V2RAYN_WEB_API_KEY=fixture-secret\n' > "$publish/.env"
  printf 'unreviewed template\n' > "$publish/.env.example"
  for executable in bin/xray/xray bin/sing_box/sing-box bin/mihomo/mihomo; do
    printf '#!/bin/sh\nexit 0\n' > "$publish/$executable"
    chmod 755 "$publish/$executable"
  done
  for asset in bin/sing_box/libcronet.so bin/geosite.dat bin/geoip.dat bin/geoip.metadb bin/Country.mmdb bin/srss/geosite-category-ads-all.srs; do
    printf 'asset\n' > "$publish/$asset"
  done
  # User data that must never leak into the app-only self-update package.
  mkdir -p "$publish/guiConfigs" "$publish/guiLogs" "$publish/webData"
  printf 'config\n' > "$publish/guiConfigs/guiNConfig.json"
  printf 'log\n' > "$publish/guiLogs/app.log"
  printf 'auth\n' > "$publish/webData/web-auth.json"
  python3 - "$publish/v2rayN.Web.build.json" "$rid" <<'PY'
import json
import sys

path, rid = sys.argv[1:]
with open(path, "w", encoding="utf-8") as handle:
    json.dump({
        "product": "v2rayN.Web",
        "version": "7.25.3",
        "commit": "0123456789abcdef",
        "buildDate": "2026-09-28T00:00:00Z",
        "rid": rid,
    }, handle, separators=(",", ":"))
    handle.write("\n")
PY
}

for rid in linux-x64 linux-arm64; do
  make_publish_fixture "$rid"
  bash "$web_root/Scripts/package-native.sh" "$rid" "$temporary/publish-$rid" "$temporary/dist"
done

full_x64="$(python3 "$asset_tool" get --rid linux-x64 --field full)"
full_arm64="$(python3 "$asset_tool" get --rid linux-arm64 --field full)"
update_x64="$(python3 "$asset_tool" get --rid linux-x64 --field update)"
update_arm64="$(python3 "$asset_tool" get --rid linux-arm64 --field update)"

for asset in "$full_x64" "$full_arm64" "$update_x64" "$update_arm64"; do
  test -s "$temporary/dist/$asset"
done
test "$full_x64" = "v2rayN-linux-64-web.zip"
test "$full_arm64" = "v2rayN-linux-arm64-web.zip"
test "$update_x64" = "v2rayN-linux-64-web-update.zip"
test "$update_arm64" = "v2rayN-linux-arm64-web-update.zip"

python3 - "$temporary/dist/$full_x64" "$temporary/dist/$update_x64" "$web_root/.env.example" <<'PY'
from pathlib import Path
import sys
import zipfile

full, update, env_example_source = sys.argv[1:]
with zipfile.ZipFile(full) as archive:
    names = archive.namelist()
    env_example_data = archive.read(".env.example")
    webui_readme_data = archive.read("webui/README.txt")
for required in ("v2rayN.Web", "v2rayN.Web.build.json", ".env.example", "webui/README.txt", "bin/xray/xray"):
    assert required in names, required
assert names.count(".env.example") == 1, names
assert env_example_data == Path(env_example_source).read_bytes()
assert webui_readme_data.startswith(b"This API package does not include a WebUI."), webui_readme_data
assert not any(name.startswith(("wwwroot/", "webui/")) and name != "webui/README.txt" for name in names), names
assert not any(".env" in name.split("/") for name in names), names
assert not any(".env.example" in name.split("/") and name != ".env.example" for name in names), names
with zipfile.ZipFile(update) as archive:
    update_names = set(archive.namelist())
assert update_names == {"v2rayN.Web", "v2rayN.Web.build.json"}, update_names
assert not any(segment in (".env", ".env.example") for name in update_names for segment in name.split("/")), update_names
assert not any(name.startswith(("bin/", "guiConfigs/", "guiLogs/", "webData/")) for name in update_names), update_names
process = __import__("subprocess").run(["unzip", "-l", update], capture_output=True, text=True)
assert process.returncode == 0, process.stderr

web_root = Path(env_example_source).parent
web_ignore = (web_root / ".gitignore").read_text(encoding="utf-8").splitlines()
container_ignore = (web_root / "Containerfile.dockerignore").read_text(encoding="utf-8").splitlines()
assert ".env" in web_ignore, web_ignore
assert ".env" in container_ignore and "**/.env" in container_ignore, container_ignore
print("ZIP boundary assertions passed")
PY

python3 "$web_root/Scripts/web-update-manifest.py" write \
  --version 7.25.3 \
  --repository 2dust/v2rayN \
  --commit 0123456789abcdef \
  --build-date 2026-09-28T00:00:00Z \
  --dist "$temporary/dist" \
  --output "$temporary/dist/web-update.json"
python3 "$web_root/Scripts/web-update-manifest.py" verify \
  --manifest "$temporary/dist/web-update.json" \
  --dist "$temporary/dist" \
  --repository 2dust/v2rayN

python3 - "$temporary/dist/web-update.json" "$update_x64" "$update_arm64" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
asset_x64, asset_arm64 = sys.argv[2:]
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
assert manifest["product"] == "v2rayN.Web", manifest
assert manifest["version"] == "7.25.3", manifest
assert manifest["commit"] == "0123456789abcdef", manifest
assert manifest["buildDate"] == "2026-09-28T00:00:00Z", manifest
expected = {
    "linux-x64": asset_x64,
    "linux-arm64": asset_arm64,
}
packages = {package["rid"]: package for package in manifest["packages"]}
assert set(packages) == set(expected), packages
for rid, asset in expected.items():
    package = packages[rid]
    url = f"https://github.com/2dust/v2rayN/releases/download/7.25.3/{asset}"
    assert asset.startswith("v2rayN-linux-") and asset.endswith("-web-update.zip"), asset
    assert package["asset"] == asset, package
    assert package["url"] == url, package
    data = (manifest_path.parent / asset).read_bytes()
    assert package["sha256"] == hashlib.sha256(data).hexdigest(), package
    assert package["size"] == len(data), package
print("web-update.json assertions passed")
PY

# A dev identity must never become a release manifest.
if python3 "$web_root/Scripts/web-update-manifest.py" write \
  --version 7.25.3-dev \
  --repository 2dust/v2rayN \
  --commit 0123456789abcdef \
  --build-date 2026-09-28T00:00:00Z \
  --dist "$temporary/dist" \
  --output "$temporary/dev-update.json" 2>/dev/null; then
  echo "Expected the dev identity manifest to be rejected" >&2
  exit 1
fi
test ! -e "$temporary/dev-update.json"

# A tampered asset must fail SHA-256 verification.
cp -a "$temporary/dist" "$temporary/tampered"
printf 'tampered' >> "$temporary/tampered/$update_x64"
cp -a "$temporary/dist/web-update.json" "$temporary/tampered/web-update.json"
if python3 "$web_root/Scripts/web-update-manifest.py" verify \
  --manifest "$temporary/tampered/web-update.json" \
  --dist "$temporary/tampered" \
  --repository 2dust/v2rayN 2>/dev/null; then
  echo "Expected the tampered manifest to be rejected" >&2
  exit 1
fi

# The embedded v2rayN.Web.build.json must match the manifest, and the app-only ZIP boundary
# must reject forbidden files, links, duplicates, and path traversal. Each variant regenerates
# the manifest from the tampered archive so SHA-256 and size stay self-consistent and only the
# identity/layout cross-check can fail.
python3 - "$temporary/dist" "$temporary/identity-tamper" "$update_x64" <<'PY'
import json
import shutil
import sys
import warnings
import zipfile
from pathlib import Path

warnings.filterwarnings("ignore", message="Duplicate name:.*")

dist = Path(sys.argv[1])
out_root = Path(sys.argv[2])
source = dist / sys.argv[3]
identity_name = "v2rayN.Web.build.json"
fixed_time = (1980, 1, 1, 0, 0, 0)

with zipfile.ZipFile(source) as archive:
    base = [(info, archive.read(info)) for info in archive.infolist()]

identity = json.loads(next(data for info, data in base if info.filename == identity_name))
identity_data = next(data for info, data in base if info.filename == identity_name)
untouched = [path for path in dist.iterdir() if path.is_file() and path.name.endswith(".zip") and path != source]


def write_variant(name, transform):
    target = out_root / name
    target.mkdir(parents=True, exist_ok=True)
    for path in untouched:
        shutil.copy2(path, target / path.name)
    with zipfile.ZipFile(target / source.name, "w", zipfile.ZIP_DEFLATED) as archive:
        for info, data in transform(list(base)):
            archive.writestr(info, data)


def identity_transform(field=None, value=None, drop=False, invalid=False):
    def transform(entries):
        result = []
        for info, data in entries:
            if info.filename != identity_name:
                result.append((info, data))
                continue
            if drop:
                continue
            if invalid:
                result.append((info, b"{ not valid json"))
                continue
            changed = dict(identity)
            changed[field] = value
            result.append((info, json.dumps(changed, separators=(",", ":")).encode("utf-8")))
        return result
    return transform


def appended(name, data, symlink=False):
    def transform(entries):
        info = zipfile.ZipInfo(name, date_time=fixed_time)
        info.create_system = 3
        file_type = 0o120000 if symlink else 0o100000
        info.external_attr = (file_type | 0o755) << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        return entries + [(info, data)]
    return transform


write_variant("version", identity_transform(field="version", value="7.25.4"))
write_variant("commit", identity_transform(field="commit", value="ffffffffffffffff"))
write_variant("build-date", identity_transform(field="buildDate", value="2026-09-29T00:00:00Z"))
write_variant("rid", identity_transform(field="rid", value="linux-arm64"))
write_variant("missing-identity", identity_transform(drop=True))
write_variant("invalid-json", identity_transform(invalid=True))
write_variant("embedded-bin", appended("bin/xray/xray", b"#!/bin/sh\nexit 0\n"))
write_variant("symlink", appended("wwwroot/vendor.js", b"/etc/passwd", symlink=True))
write_variant("duplicate", appended(identity_name, identity_data))
write_variant("traversal", appended("../outside.txt", b"nope"))
print(f"prepared identity tamper variants in {out_root}")
PY

for variant in \
  version commit build-date rid missing-identity invalid-json \
  embedded-bin symlink duplicate traversal; do
  if python3 "$web_root/Scripts/web-update-manifest.py" write \
    --version 7.25.3 \
    --repository 2dust/v2rayN \
    --commit 0123456789abcdef \
    --build-date 2026-09-28T00:00:00Z \
    --dist "$temporary/identity-tamper/$variant" \
    --output "$temporary/identity-tamper/$variant/web-update.json" \
    2>"$temporary/identity-tamper/$variant.log"; then
    echo "Expected the $variant identity tamper to be rejected" >&2
    exit 1
  fi
  if ! grep -q "app-only archive $update_x64" "$temporary/identity-tamper/$variant.log"; then
    echo "The $variant identity tamper was rejected without naming the app-only archive" >&2
    cat "$temporary/identity-tamper/$variant.log" >&2
    exit 1
  fi
done

printf 'Release packaging dry-run passed\n'
