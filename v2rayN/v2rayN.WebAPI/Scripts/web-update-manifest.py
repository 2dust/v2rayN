#!/usr/bin/env python3
"""Build or verify the v2rayN Web `web-update.json` release manifest.

The manifest is published next to the official v2rayN Release assets. Every URL points at the
official release tag in the repository that produced the assets, so the Web self-update never
depends on a fork-specific release channel.

Verification also opens each app-only archive and cross-checks its embedded
`v2rayN.WebAPI.build.json` against the manifest. App-only archives contain only the API executable
and build identity; user-installed WebUI files are neither packaged nor touched by self-update.

Usage:
  web-update-manifest.py write --version 7.25.4 --repository 2dust/v2rayN --commit <sha> \
      --build-date 2026-09-28T00:00:00Z --dist dist --output dist/web-update.json
  web-update-manifest.py verify --manifest dist/web-update.json --dist dist [--repository 2dust/v2rayN]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path

PRODUCT = "v2rayN.WebAPI"
ASSET_MAP_PATH = Path(__file__).resolve().parent.parent / "Assets" / "web-assets.json"
BUILD_IDENTITY_NAME = "v2rayN.WebAPI.build.json"
APP_EXECUTABLE_NAME = "v2rayN.WebAPI"
MAX_ARCHIVE_ENTRIES = 10000
MAX_EXPANDED_ARCHIVE_BYTES = 1024 * 1024 * 1024
VERSION_RE = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+$")
OFFICIAL_URL_RE = re.compile(
    r"^https://github\.com/[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+/releases/download/(?P<tag>[^/]+)/(?P<asset>[^/]+)$"
)


def fail(message: str) -> None:
    print(f"web-update manifest error: {message}", file=sys.stderr)
    raise SystemExit(1)


def load_assets() -> dict[str, dict[str, str]]:
    """Read the RID-to-asset map shared with the packager and the embedded runtime map."""
    try:
        data = json.loads(ASSET_MAP_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"could not read {ASSET_MAP_PATH}: {exc}")
    if not isinstance(data, dict) or not isinstance(data.get("assets"), list):
        fail(f"{ASSET_MAP_PATH} is missing its assets list")
    assets: dict[str, dict[str, str]] = {}
    for entry in data["assets"]:
        if not isinstance(entry, dict):
            fail(f"{ASSET_MAP_PATH} contains a non-object asset entry")
        values = (entry.get("rid"), entry.get("arch"), entry.get("full"), entry.get("update"))
        if not all(isinstance(value, str) and value for value in values) or values[0] in assets:
            fail(f"{ASSET_MAP_PATH} contains an invalid or duplicate asset entry")
        assets[values[0]] = {"arch": values[1], "full": values[2], "update": values[3]}
    if not assets:
        fail(f"{ASSET_MAP_PATH} contains no assets")
    return assets


ASSETS = load_assets()


def full_asset_name(rid: str) -> str:
    return ASSETS[rid]["full"]


def app_asset_name(rid: str) -> str:
    return ASSETS[rid]["update"]


def asset_url(repository: str, version: str, asset: str) -> str:
    return f"https://github.com/{repository}/releases/download/{version}/{asset}"


def require_release_files(dist: Path) -> None:
    for rid in ASSETS:
        for asset in (full_asset_name(rid), app_asset_name(rid)):
            path = dist / asset
            if not path.is_file() or path.stat().st_size == 0:
                fail(f"release asset is missing or empty: {path}")


def normalize_member_path(archive_name: str, member_name: str) -> str:
    """Normalize an archive entry exactly like the Web runtime package stager does."""
    if not member_name or member_name.startswith("/") or "\\" in member_name or "\0" in member_name:
        fail(f"app-only archive {archive_name} contains an invalid archive path: {member_name!r}")
    segments = [segment for segment in member_name.split("/") if segment]
    if not segments or any(segment in (".", "..") or ":" in segment for segment in segments):
        fail(f"app-only archive {archive_name} contains an invalid archive path: {member_name!r}")
    return "/".join(segments)


def app_member_allowed(path: str) -> bool:
    return path in (APP_EXECUTABLE_NAME, BUILD_IDENTITY_NAME)


def verify_app_archive_identity(archive: Path, rid: str, version: str, commit: str, build_date: str) -> None:
    """Cross-check the app-only ZIP layout and its embedded build identity.

    The archive must only carry the executable and its `v2rayN.WebAPI.build.json` identity. The
    identity must match the release manifest exactly, mirroring the checks the Web runtime
    performs before installing a self-update package.
    """
    archive_name = archive.name
    seen: set[str] = set()
    identity_bytes: bytes | None = None
    entry_count = 0
    expanded_bytes = 0
    try:
        handle = zipfile.ZipFile(archive)
    except (OSError, zipfile.BadZipFile) as exc:
        fail(f"app-only archive {archive_name} could not be read: {exc}")
    with handle:
        for info in handle.infolist():
            entry_count += 1
            if entry_count > MAX_ARCHIVE_ENTRIES:
                fail(f"app-only archive {archive_name} has more than {MAX_ARCHIVE_ENTRIES} entries")
            path = normalize_member_path(archive_name, info.filename)
            if path in seen:
                fail(f"app-only archive {archive_name} contains a duplicate archive path: {path}")
            seen.add(path)
            if not app_member_allowed(path):
                fail(f"app-only archive {archive_name} contains an entry outside the app-only layout: {path}")
            unix_type = (info.external_attr >> 16) & 0xF000
            if unix_type not in (0, 0x4000, 0x8000):
                fail(f"app-only archive {archive_name} contains a link or special entry: {path}")
            if unix_type == 0x4000 or info.filename.endswith("/"):
                continue
            expanded_bytes += info.file_size
            if expanded_bytes > MAX_EXPANDED_ARCHIVE_BYTES:
                fail(f"app-only archive {archive_name} expands beyond the supported size")
            if path == BUILD_IDENTITY_NAME:
                identity_bytes = handle.read(info)

    for required in (APP_EXECUTABLE_NAME, BUILD_IDENTITY_NAME):
        if required not in seen:
            fail(f"app-only archive {archive_name} is missing {required}")
    if identity_bytes is None:
        fail(f"app-only archive {archive_name} has an unreadable {BUILD_IDENTITY_NAME}")
    try:
        identity = json.loads(identity_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"app-only archive {archive_name} has an invalid {BUILD_IDENTITY_NAME}: {exc}")
    if not isinstance(identity, dict):
        fail(f"app-only archive {archive_name} has an invalid {BUILD_IDENTITY_NAME}: expected a JSON object")

    expected = {"product": PRODUCT, "version": version, "commit": commit, "buildDate": build_date, "rid": rid}
    for field, value in expected.items():
        actual = identity.get(field)
        if actual != value:
            fail(
                f"app-only archive {archive_name} identity mismatch for {field}: "
                f"{actual!r} does not match the manifest value {value!r}"
            )


def package_entry(dist: Path, repository: str, version: str, rid: str) -> dict:
    path = dist / app_asset_name(rid)
    data = path.read_bytes()
    return {
        "rid": rid,
        "asset": path.name,
        "url": asset_url(repository, version, path.name),
        "sha256": hashlib.sha256(data).hexdigest(),
        "size": len(data),
    }


def build_manifest(dist: Path, version: str, repository: str, commit: str, build_date: str) -> dict:
    if not VERSION_RE.match(version):
        fail(f"official release versions must be x.y.z, got: {version}")
    if not REPOSITORY_RE.match(repository):
        fail(f"invalid release repository: {repository}")
    if not commit or commit == "unknown":
        fail("the manifest requires a real commit identity")
    if not build_date or build_date == "unknown":
        fail("the manifest requires a real build date")
    require_release_files(dist)
    return {
        "product": PRODUCT,
        "version": version,
        "commit": commit,
        "buildDate": build_date,
        "packages": [package_entry(dist, repository, version, rid) for rid in ASSETS],
    }


def verify_manifest(manifest_path: Path, dist: Path, repository: str | None, version: str | None = None) -> None:
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"could not read {manifest_path}: {exc}")
    if not isinstance(manifest, dict) or manifest.get("product") != PRODUCT:
        fail(f"{manifest_path} is not a {PRODUCT} manifest")
    manifest_version = manifest.get("version")
    if not isinstance(manifest_version, str) or not VERSION_RE.match(manifest_version):
        fail(f"{manifest_path} has an invalid version: {manifest_version!r}")
    if version is not None and manifest_version != version:
        fail(f"{manifest_path} version {manifest_version} does not match {version}")
    if not isinstance(manifest.get("commit"), str) or not manifest["commit"]:
        fail(f"{manifest_path} has no commit identity")
    if not isinstance(manifest.get("buildDate"), str) or not manifest["buildDate"]:
        fail(f"{manifest_path} has no build date")

    packages = manifest.get("packages")
    if not isinstance(packages, list) or len(packages) != len(ASSETS):
        fail(f"{manifest_path} must contain exactly {len(ASSETS)} packages")
    by_rid = {}
    for package in packages:
        if not isinstance(package, dict):
            fail(f"{manifest_path} contains a non-object package entry")
        rid = package.get("rid")
        if rid not in ASSETS:
            fail(f"{manifest_path} contains an unsupported runtime identifier: {rid!r}")
        if rid in by_rid:
            fail(f"{manifest_path} contains duplicate runtime identifiers")
        asset = package.get("asset")
        if asset != app_asset_name(rid):
            fail(f"{manifest_path} has an unexpected asset for {rid}: {asset!r}")
        path = dist / asset
        if not path.is_file() or path.stat().st_size == 0:
            fail(f"release asset is missing or empty: {path}")
        data = path.read_bytes()
        if package.get("sha256") != hashlib.sha256(data).hexdigest():
            fail(f"sha256 mismatch for {asset}")
        if package.get("size") != len(data):
            fail(f"size mismatch for {asset}")
        url = package.get("url")
        if not isinstance(url, str):
            fail(f"missing asset URL for {rid}")
        match = OFFICIAL_URL_RE.match(url)
        if not match or match.group("tag") != manifest_version or match.group("asset") != asset:
            fail(f"asset URL is not an official release URL for this tag: {url}")
        if repository is not None and not url.startswith(f"https://github.com/{repository}/"):
            fail(f"asset URL does not use repository {repository}: {url}")
        verify_app_archive_identity(path, rid, manifest_version, manifest["commit"], manifest["buildDate"])
        by_rid[rid] = package

    for rid in ASSETS:
        if rid not in by_rid:
            fail(f"{manifest_path} is missing runtime {rid}")
    require_release_files(dist)


def write_manifest(args: argparse.Namespace) -> None:
    dist = Path(args.dist)
    manifest = build_manifest(dist, args.version, args.repository, args.commit, args.build_date)
    output = Path(args.output)
    output.write_text(json.dumps(manifest, separators=(",", ":")) + "\n", encoding="utf-8")
    verify_manifest(output, dist, args.repository, args.version)
    print(f"Wrote {output} for {PRODUCT} {args.version} ({args.repository})")


def verify_command(args: argparse.Namespace) -> None:
    verify_manifest(Path(args.manifest), Path(args.dist), args.repository)
    print(f"Verified {args.manifest}")


def main(argv: list[str]) -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    subparsers = parser.add_subparsers(dest="command", required=True)

    write_parser = subparsers.add_parser("write", help="write web-update.json from the packaged assets")
    write_parser.add_argument("--version", required=True)
    write_parser.add_argument("--repository", required=True)
    write_parser.add_argument("--commit", required=True)
    write_parser.add_argument("--build-date", required=True)
    write_parser.add_argument("--dist", required=True)
    write_parser.add_argument("--output", required=True)
    write_parser.set_defaults(func=write_manifest)

    verify_parser = subparsers.add_parser("verify", help="verify web-update.json against the packaged assets")
    verify_parser.add_argument("--manifest", required=True)
    verify_parser.add_argument("--dist", required=True)
    verify_parser.add_argument("--repository")
    verify_parser.set_defaults(func=verify_command)

    args = parser.parse_args(argv)
    args.func(args)


if __name__ == "__main__":
    main(sys.argv[1:])
