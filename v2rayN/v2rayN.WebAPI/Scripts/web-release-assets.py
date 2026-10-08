#!/usr/bin/env python3
"""Package the headless v2rayN Web API release ZIPs and report their asset names.

The RID-to-asset map lives in `../Assets/web-assets.json`. The Web assembly embeds the same
file, so the runtime updater, the release manifest, and this packager never disagree about the
release asset names:

  linux-x64   -> v2rayN-linux-64-web.zip        + v2rayN-linux-64-web-update.zip
  linux-arm64 -> v2rayN-linux-arm64-web.zip     + v2rayN-linux-arm64-web-update.zip

Usage:
  web-release-assets.py get --rid linux-x64 --field full
  web-release-assets.py package --rid linux-x64 --publish publish/linux-x64 --dist dist
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import stat
import sys
import zipfile
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
ASSET_MAP_PATH = SCRIPT_DIR.parent / "Assets" / "web-assets.json"
FIXED_TIME = (1980, 1, 1, 0, 0, 0)
EXECUTABLE_NAME = "v2rayN.WebAPI"
BUILD_IDENTITY_NAME = "v2rayN.WebAPI.build.json"
ENVIRONMENT_FILE_NAMES = frozenset((".env", ".env.example"))
REQUIRED_FILES = (
    EXECUTABLE_NAME,
    BUILD_IDENTITY_NAME,
    "bin/sing_box/libcronet.so",
    "bin/geosite.dat",
    "bin/geoip.dat",
    "bin/geoip.metadb",
    "bin/Country.mmdb",
    "bin/srss/geosite-category-ads-all.srs",
)
REQUIRED_EXECUTABLES = (
    EXECUTABLE_NAME,
    "bin/xray/xray",
    "bin/sing_box/sing-box",
    "bin/mihomo/mihomo",
)
BLOCK_SIZE = 1024 * 1024


def fail(message: str) -> None:
    print(f"web release assets error: {message}", file=sys.stderr)
    raise SystemExit(1)


def load_assets() -> dict[str, dict[str, str]]:
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


def normalize_entry(name: str) -> str:
    """Normalize an archive entry name the same way the Web runtime package stager does."""
    if not name or name.startswith("/") or "\\" in name or "\0" in name:
        fail(f"invalid archive path: {name!r}")
    segments = [segment for segment in name.split("/") if segment]
    if not segments or any(segment in (".", "..") or ":" in segment for segment in segments):
        fail(f"invalid archive path: {name!r}")
    return "/".join(segments)


def collect_tree(root: Path) -> list[tuple[str, Path, str]]:
    entries: list[tuple[str, Path, str]] = []
    for current, dirnames, filenames in os.walk(root, followlinks=False):
        dirnames.sort()
        filenames.sort()
        for name in list(dirnames):
            path = Path(current) / name
            if path.is_symlink():
                entries.append((path.relative_to(root).as_posix(), path, "symlink"))
                dirnames.remove(name)
        for name in filenames:
            path = Path(current) / name
            if path.is_symlink():
                entries.append((path.relative_to(root).as_posix(), path, "symlink"))
            elif path.is_file():
                entries.append((path.relative_to(root).as_posix(), path, "file"))
            else:
                fail(f"unsupported file type in the Web package: {path}")
    return entries


def write_file(handle: zipfile.ZipFile, source: Path, arcname: str, mode: int) -> None:
    info = zipfile.ZipInfo(arcname, date_time=FIXED_TIME)
    info.create_system = 3
    info.external_attr = ((stat.S_IFREG | mode) & 0xFFFF) << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    with source.open("rb") as reader, handle.open(info, "w") as writer:
        shutil.copyfileobj(reader, writer, BLOCK_SIZE)


def write_symlink(handle: zipfile.ZipFile, target: str, arcname: str) -> None:
    info = zipfile.ZipInfo(arcname, date_time=FIXED_TIME)
    info.create_system = 3
    info.external_attr = (stat.S_IFLNK | 0o777) << 16
    info.compress_type = zipfile.ZIP_STORED
    handle.writestr(info, target)


def create_full_zip(publish: Path, output: Path) -> None:
    env_example = SCRIPT_DIR.parent / ".env.example"
    if env_example.is_symlink() or not env_example.is_file():
        fail(f"full package is missing the source environment example: {env_example}")

    # Only the reviewed template is shipped. A local .env (or another copied template) is
    # never taken from the publish tree, even if a build tool happened to copy it there.
    entries = [
        entry for entry in collect_tree(publish)
        if not any(segment in ENVIRONMENT_FILE_NAMES for segment in entry[0].split("/"))
    ]
    names = {relative for relative, _, _ in entries}
    for required in REQUIRED_FILES:
        if required not in names:
            fail(f"full package is missing required file: {required}")
    for required in REQUIRED_EXECUTABLES:
        if required not in names or not os.access(publish / required, os.X_OK):
            fail(f"full package is missing required executable: {required}")
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, allowZip64=True) as handle:
        for relative, path, kind in entries:
            if kind == "symlink":
                write_symlink(handle, os.readlink(path), relative)
            else:
                write_file(handle, path, relative, stat.S_IMODE(path.stat().st_mode))
        write_file(handle, env_example, ".env.example", 0o644)


def create_update_zip(publish: Path, output: Path) -> None:
    if not os.access(publish / EXECUTABLE_NAME, os.X_OK):
        fail(f"app-only package is missing required executable: {EXECUTABLE_NAME}")
    entries: list[tuple[str, Path]] = []
    for name in (EXECUTABLE_NAME, BUILD_IDENTITY_NAME):
        path = publish / name
        if path.is_symlink() or not path.is_file():
            fail(f"app-only package is missing required file: {name}")
    entries.append((EXECUTABLE_NAME, publish / EXECUTABLE_NAME))
    entries.append((BUILD_IDENTITY_NAME, publish / BUILD_IDENTITY_NAME))

    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, allowZip64=True) as handle:
        for arcname, path in entries:
            write_file(handle, path, arcname, stat.S_IMODE(path.stat().st_mode))
    verify_update_zip(output)


def verify_update_zip(path: Path) -> None:
    try:
        handle = zipfile.ZipFile(path)
    except (OSError, zipfile.BadZipFile) as exc:
        fail(f"app-only package {path.name} could not be read: {exc}")
    with handle:
        seen: set[str] = set()
        for info in handle.infolist():
            if (info.external_attr >> 16) & 0xF000 not in (0, 0x4000, 0x8000):
                fail(f"app-only package {path.name} contains a link or special entry: {info.filename}")
            name = normalize_entry(info.filename)
            if any(segment in ENVIRONMENT_FILE_NAMES for segment in name.split("/")):
                fail(f"app-only package {path.name} must not contain environment files: {name}")
            if name in seen:
                fail(f"app-only package {path.name} contains a duplicate archive path: {name}")
            seen.add(name)
            if name not in (EXECUTABLE_NAME, BUILD_IDENTITY_NAME):
                fail(f"app-only package {path.name} contains an unexpected entry: {name}")
        for required in (EXECUTABLE_NAME, BUILD_IDENTITY_NAME):
            if required not in seen:
                fail(f"app-only package {path.name} is missing {required}")


def get_command(args: argparse.Namespace) -> None:
    assets = load_assets()
    entry = assets.get(args.rid)
    if entry is None:
        fail(f"unsupported runtime identifier: {args.rid}")
    print(entry[args.field])


def package_command(args: argparse.Namespace) -> None:
    assets = load_assets()
    entry = assets.get(args.rid)
    if entry is None:
        fail(f"unsupported runtime identifier: {args.rid}")
    publish = Path(args.publish)
    dist = Path(args.dist)
    if not publish.is_dir():
        fail(f"publish directory does not exist: {publish}")
    dist.mkdir(parents=True, exist_ok=True)
    full_archive = dist / entry["full"]
    update_archive = dist / entry["update"]
    create_full_zip(publish, full_archive)
    create_update_zip(publish, update_archive)
    print(f"Packaged {full_archive} and {update_archive}")


def main(argv: list[str]) -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    subparsers = parser.add_subparsers(dest="command", required=True)

    get_parser = subparsers.add_parser("get", help="print a shared release asset name")
    get_parser.add_argument("--rid", required=True)
    get_parser.add_argument("--field", required=True, choices=("arch", "full", "update"))
    get_parser.set_defaults(func=get_command)

    package_parser = subparsers.add_parser("package", help="build the full and app-only release ZIPs")
    package_parser.add_argument("--rid", required=True)
    package_parser.add_argument("--publish", required=True)
    package_parser.add_argument("--dist", required=True)
    package_parser.set_defaults(func=package_command)

    args = parser.parse_args(argv)
    args.func(args)


if __name__ == "__main__":
    main(sys.argv[1:])
