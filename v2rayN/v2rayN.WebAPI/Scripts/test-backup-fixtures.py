#!/usr/bin/env python3
"""Exercise real Desktop/Web backup ZIPs without printing their private contents."""

from __future__ import annotations

import base64
import json
import os
import socket
import sqlite3
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path


WEB_ROOT = Path(__file__).resolve().parents[1]
PROJECT = WEB_ROOT / "v2rayN.WebAPI.csproj"
DOTNET = os.environ.get("DOTNET", str(WEB_ROOT / ".NET" / "dotnet"))
API_KEY = "desktop-backup-integration-management-key"


class FixtureFailure(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise FixtureFailure(message)


def free_port() -> int:
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


class WebHost:
    def __init__(self, data_home: Path, port: int):
        self.data_home = data_home
        self.port = port
        self.base = f"http://127.0.0.1:{port}"
        self.process: subprocess.Popen[bytes] | None = None
        self.mode = "foreground"
        self.owner_pid: int | None = None
        self.log_path = data_home.parent / "web-host.log"
        self.log_stream = None

    def start(self, mode: str = "foreground") -> None:
        self.mode = mode
        environment = os.environ.copy()
        environment["V2RAYN_DATA_HOME"] = str(self.data_home)
        environment["V2RAYN_WEB_AUTOSTART"] = "false"
        environment.pop("V2RAYN_WEB_API_KEY", None)
        if self.log_stream is None:
            self.log_stream = self.log_path.open("ab", buffering=0)
        self.process = subprocess.Popen(
            [DOTNET, "run", "--project", str(PROJECT), "--no-build", "--",
             "--background-child" if mode == "background-child" else "--foreground",
             "--urls", self.base],
            cwd=WEB_ROOT.parent,
            env=environment,
            stdout=self.log_stream,
            stderr=subprocess.STDOUT,
        )
        deadline = time.monotonic() + 35
        while time.monotonic() < deadline:
            if self.process.poll() is not None:
                raise FixtureFailure("Web host exited before becoming healthy.")
            try:
                status, _ = self.request("GET", "/api/health")
                if status == 200:
                    self.owner_pid = self.current_owner_pid()
                    return
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(0.15)
        raise FixtureFailure("Web host did not become healthy within 35 seconds.")

    def stop_for_restore(self) -> None:
        process = self.process
        if process is None:
            raise FixtureFailure("No host process is active.")
        try:
            process.wait(timeout=35)
        except subprocess.TimeoutExpired as error:
            process.send_signal(15)
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                raise FixtureFailure("Restored Web host did not exit after its graceful shutdown.") from error
        if self.log_stream is not None:
            self.log_stream.flush()
        if process.returncode != 0 and self.log_path.exists():
            diagnostics = [line for line in self.log_path.read_text(errors="replace").splitlines()
                           if any(marker in line.lower() for marker in ("exception", "fatal", "unhandled", "abort", "failed"))]
            for line in diagnostics[-20:]:
                print(f"Web diagnostic: {line[:500]}", file=sys.stderr)
        require(process.returncode == 0, f"Restore-triggered Web host exit was not successful (code {process.returncode}).")
        self.process = None

    def stop_current_gracefully(self) -> None:
        process = self.process
        if process is None:
            return
        process.send_signal(15)
        try:
            process.wait(timeout=35)
        except subprocess.TimeoutExpired as error:
            raise FixtureFailure("Foreground fixture Web host did not stop gracefully.") from error
        require(process.returncode == 0, f"Foreground fixture host stopped with code {process.returncode}.")
        self.process = None
        self.owner_pid = None

    def wait_native_relaunch(self) -> None:
        process = self.process
        previous_owner = self.owner_pid
        if process is not None:
            try:
                process.wait(timeout=35)
            except subprocess.TimeoutExpired as error:
                raise FixtureFailure("The native instance did not exit to complete its requested restart.") from error
            require(process.returncode == 0, f"Native restore host exited with code {process.returncode}.")
            self.process = None
        require(previous_owner is not None, "The native instance lock did not record an owner process.")
        deadline = time.monotonic() + 35
        while time.monotonic() < deadline:
            try:
                status, _ = self.request("GET", "/api/health")
                new_owner = self.current_owner_pid()
                if status == 200 and new_owner is not None and new_owner != previous_owner:
                    self.owner_pid = new_owner
                    return
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(0.15)
        raise FixtureFailure("The native launcher did not restart Web after restore.")

    def current_owner_pid(self) -> int | None:
        locks = list(self.data_home.rglob("v2rayN.WebAPI.instance.lock"))
        if not locks:
            return None
        try:
            value = int(locks[0].read_text().strip())
            return value if value > 0 else None
        except (OSError, ValueError):
            return None

    def stop_native_via_cli(self) -> None:
        result = subprocess.run(
            [DOTNET, "run", "--project", str(PROJECT), "--no-build", "--", "--stop", "--urls", self.base],
            cwd=WEB_ROOT.parent,
            env={**os.environ, "V2RAYN_DATA_HOME": str(self.data_home), "V2RAYN_WEB_AUTOSTART": "false"},
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=40,
            check=False,
        )
        require(result.returncode == 0, "Native --stop did not confirm actual Web/Core shutdown.")

    def close(self) -> None:
        if self.process is not None and self.process.poll() is None:
            self.process.send_signal(15)
            try:
                self.process.wait(timeout=35)
            except subprocess.TimeoutExpired as error:
                raise FixtureFailure("Web host did not stop gracefully during fixture cleanup.") from error
        self.process = None
        if self.mode == "background-child":
            try:
                self.stop_native_via_cli()
            except FixtureFailure:
                # Preserve the originating test error, but never silently detach the test host.
                pass
        if self.log_stream is not None:
            self.log_stream.close()
            self.log_stream = None

    def request(self, method: str, path: str, payload: object | None = None,
                token: str | None = None, content_type: str | None = None,
                raw_body: bytes | None = None) -> tuple[int, object | bytes]:
        headers: dict[str, str] = {}
        if token:
            headers["Authorization"] = f"Bearer {token}"
        body = raw_body
        if payload is not None:
            body = json.dumps(payload).encode()
            headers["Content-Type"] = content_type or "application/json"
        if content_type:
            headers["Content-Type"] = content_type
        request = urllib.request.Request(self.base + path, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                content = response.read()
                if response.headers.get_content_type() == "application/zip":
                    return response.status, content
                return response.status, json.loads(content) if content else {}
        except urllib.error.HTTPError as error:
            content = error.read()
            try:
                parsed: object | bytes = json.loads(content) if content else {}
            except json.JSONDecodeError:
                parsed = content
            return error.code, parsed

    def login(self) -> str:
        status, result = self.request("POST", "/api/auth/login", {"key": API_KEY})
        require(status == 200, "The original Web management key did not survive restore.")
        return result["data"]["token"]

    def restore(self, archive: Path, token: str) -> None:
        boundary = f"----v2rayn-fixture-{uuid.uuid4().hex}"
        body = (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{archive.name}"\r\n'
            "Content-Type: application/zip\r\n\r\n"
        ).encode() + archive.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
        status, result = self.request(
            "POST", "/api/backup/restore", token=token,
            content_type=f"multipart/form-data; boundary={boundary}", raw_body=body)
        require(status == 202 and result.get("success") is True, "Backup restore endpoint rejected a real fixture.")


def wait_restart(host: WebHost, archive: Path, token: str) -> None:
    host.restore(archive, token)
    if host.mode == "background-child":
        host.wait_native_relaunch()
    else:
        host.stop_for_restore()
        host.start()


def add_auth_file_to_web_archive(source: Path, destination: Path) -> None:
    with zipfile.ZipFile(source) as original, zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as malicious:
        for entry in original.infolist():
            malicious.writestr(entry, original.read(entry.filename))
        malicious.writestr("restore-fixture/guiConfigs/web-auth.json", b"attacker-controlled-auth")


def install_fake_cores(startup_path: Path) -> None:
    lifecycle_log = repr(str(startup_path / "core-lifecycle.log"))
    drop_listener_file = repr(str(startup_path / "fake-core-drop-listener"))
    fail_once_file = repr(str(startup_path / "fake-core-fail-once-port"))
    fail_ports_file = repr(str(startup_path / "fake-core-fail-ports"))
    executable_source = f"""#!/usr/bin/python3
import json, os, re, signal, socket, sys, time
log_path = {lifecycle_log}
drop_listener_file = {drop_listener_file}
fail_once_file = {fail_once_file}
fail_ports_file = {fail_ports_file}
def record(event):
    with open(log_path, 'a', encoding='utf-8') as log:
        log.write(f"{{event}} {{os.getpid()}}\\n")
def stop(signum, frame):
    record('stop')
    raise SystemExit(0)
signal.signal(signal.SIGTERM, stop)
args = sys.argv[1:]
config_path = next(args[index + 1] for index, arg in enumerate(args[:-1]) if arg in ('-c', '-f'))
text = open(config_path, encoding='utf-8').read()
try:
    port = int(json.loads(text)['inbounds'][0]['port'])
except (json.JSONDecodeError, KeyError, IndexError, TypeError):
    port = int(re.search(r'(?m)^\\s*(?:mixed-port|port):\\s*(\\d+)', text).group(1))
if os.path.exists(fail_ports_file):
    if str(port) in open(fail_ports_file, encoding='utf-8').read().splitlines():
        record(f'failed-launch:{{port}}')
        raise SystemExit(19)
if os.path.exists(fail_once_file):
    failed_once_port = open(fail_once_file, encoding='utf-8').read().strip()
    if failed_once_port == str(port):
        os.unlink(fail_once_file)
        record(f'failed-launch-once:{{port}}')
        raise SystemExit(18)
listener = socket.socket()
listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
listener.bind(('0.0.0.0', port))
listener.listen(32)
record(f'start:{{port}}')
while True:
    if os.path.exists(drop_listener_file):
        listener.close()
        record('listener-dropped')
        while True:
            time.sleep(1)
    connection, _ = listener.accept()
    connection.close()
"""
    for core_type, executable in (("xray", "xray"), ("sing_box", "sing-box"), ("mihomo", "mihomo")):
        core_dir = startup_path / "bin" / core_type
        core_dir.mkdir(parents=True, exist_ok=True)
        binary = core_dir / executable
        binary.write_text(executable_source)
        binary.chmod(0o755)


def make_legacy_desktop_routing_archive(source: Path, destination: Path) -> tuple[str, int]:
    with zipfile.ZipFile(source) as original:
        entries = {entry.filename: original.read(entry.filename) for entry in original.infolist()}
    config_name = next(name for name in entries if name.endswith("/guiNConfig.json"))
    db_name = next(name for name in entries if name.endswith("/guiNDB.db"))
    config = json.loads(entries[config_name])
    routing_key = next(key for key in config if key.lower() == "routingbasicitem")
    routing = config[routing_key]
    index_key = next((key for key in routing if key.lower() == "routingindexid"), "RoutingIndexId")
    group_key = next(key for key in config if key.lower() == "subindexid")
    inbound_key = next(key for key in config if key.lower() == "inbound")
    inbound = config[inbound_key][0]
    port_key = next(key for key in inbound if key.lower() == "localport")
    test_port = free_port()
    inbound[port_key] = test_port

    database = sqlite3.connect(":memory:")
    database.deserialize(entries[db_name])
    route_id = database.execute("SELECT Id FROM RoutingItem ORDER BY Sort LIMIT 1").fetchone()[0]
    database.execute("UPDATE RoutingItem SET IsActive=0")
    database.commit()
    entries[db_name] = database.serialize()
    database.close()

    routing[index_key] = route_id
    config[group_key] = "missing-subscription-for-restore-regression"
    entries[config_name] = json.dumps(config, ensure_ascii=False).encode()
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as legacy:
        for name, content in entries.items():
            legacy.writestr(name, content)
    return route_id, test_port


def authorized_json(host: WebHost, token: str, method: str, path: str,
                    payload: object | None = None) -> object:
    status, result = host.request(method, path, payload, token)
    require(200 <= status < 300, f"Regression request failed: {method} {path} (HTTP {status}).")
    return result.get("data") if isinstance(result, dict) and "data" in result else result


def clone_and_crud(host: WebHost, token: str, template: dict, subscription_id: str | None) -> None:
    clone = dict(template)
    clone.update(indexId="", remarks=f"web-restore-regression-{uuid.uuid4().hex[:10]}", isSub=False)
    clone["subid"] = subscription_id or ""
    result = authorized_json(host, token, "POST", "/api/profiles", clone)
    profile_id = result.get("profileId")
    require(bool(profile_id), "Profile POST did not return the stored profile ID.")

    db_path = next(host.data_home.rglob("guiNDB.db"))
    with sqlite3.connect(db_path) as database:
        row = database.execute("SELECT Remarks, Subid, IsSub FROM ProfileItem WHERE IndexId=?", (profile_id,)).fetchone()
        require(row is not None and row[1] == (subscription_id or "") and row[2] == 0,
                "New profile was not persisted to SQLite with the selected group semantics.")

    details = authorized_json(host, token, "GET", f"/api/profiles/{urllib.parse.quote(profile_id)}")
    details["remarks"] = details["remarks"] + "-edited"
    updated = host.request("PUT", f"/api/profiles/{urllib.parse.quote(profile_id)}", details, token)[1]
    require(updated.get("success") is True, f"Profile edit failed with code {updated.get('code')}.")

    status, deleted = host.request("DELETE", "/api/profiles", {"profileIds": [profile_id]}, token)
    require(status == 200 and deleted.get("success") is True, f"Profile delete failed with code {deleted.get('code')}.")
    with sqlite3.connect(db_path) as database:
        count = database.execute("SELECT COUNT(*) FROM ProfileItem WHERE IndexId=?", (profile_id,)).fetchone()[0]
        require(count == 0, "Deleted profile remains in SQLite.")


def new_vmess_profile(subscription_id: str | None) -> dict:
    return {
        "indexId": "",
        "configType": "vMess",
        "coreType": None,
        "configVersion": 4,
        "subid": subscription_id or "",
        "isSub": False,
        "displayLog": True,
        "remarks": f"web-restore-vmess-{uuid.uuid4().hex[:10]}",
        "address": "example.invalid",
        "port": 443,
        "password": str(uuid.uuid4()),
        "username": "",
        "network": "tcp",
        "streamSecurity": "",
        "allowInsecure": "",
        "protoExtra": json.dumps({"alterId": "0", "vmessSecurity": "auto"}),
        "transportExtra": "{}",
    }


def create_and_crud(host: WebHost, token: str, profile: dict, subscription_id: str | None) -> None:
    profile["subid"] = subscription_id or ""
    result = authorized_json(host, token, "POST", "/api/profiles", profile)
    profile_id = result.get("profileId")
    require(bool(profile_id), "Profile POST did not return the stored profile ID.")
    db_path = next(host.data_home.rglob("guiNDB.db"))
    with sqlite3.connect(db_path) as database:
        row = database.execute("SELECT Subid, IsSub FROM ProfileItem WHERE IndexId=?", (profile_id,)).fetchone()
        require(row is not None and row[0] == (subscription_id or "") and row[1] == 0,
                "New VMess node was not persisted with the selected group semantics.")
    details = authorized_json(host, token, "GET", f"/api/profiles/{urllib.parse.quote(profile_id)}")
    details["remarks"] += "-edited"
    updated = host.request("PUT", f"/api/profiles/{urllib.parse.quote(profile_id)}", details, token)[1]
    require(updated.get("success") is True, f"VMess edit failed with code {updated.get('code')}.")
    status, deleted = host.request("DELETE", "/api/profiles", {"profileIds": [profile_id]}, token)
    require(status == 200 and deleted.get("success") is True, f"VMess delete failed with code {deleted.get('code')}.")


def settings_apply_body(settings: dict) -> dict:
    return {
        "inbound": dict(settings["inbound"]),
        "core": dict(settings["core"]),
        "application": dict(settings["app"]),
        "speedTest": dict(settings["speedTest"]),
        "coreTypes": settings["coreTypes"],
        "domainStrategy": settings["domainStrategy"],
        "domainStrategy4Singbox": settings["domainStrategy4Singbox"],
    }


def apply_all_settings(host: WebHost, token: str, body: dict) -> dict:
    status, result = host.request("PUT", "/api/settings/apply", body, token)
    require(status == 200 and result.get("success") is True,
            f"Atomic settings apply failed with code {result.get('code')}.")
    return result


def exercise_desktop_restore(host: WebHost, token: str) -> str:
    status = authorized_json(host, token, "GET", "/api/status")
    require(status["coreRunning"] is False, "Restore unexpectedly started a Core that was stopped before restore.")
    require(status["profileCount"] == 123 and status["subscriptionCount"] == 3,
            "Desktop backup Config/SQLite rows were not loaded after restart.")

    subscriptions = authorized_json(host, token, "GET", "/api/subscriptions")
    dns_profiles = authorized_json(host, token, "GET", "/api/settings/dns/profiles")
    routes = authorized_json(host, token, "GET", "/api/settings/routing-profiles")
    groups = authorized_json(host, token, "GET", "/api/profile-groups")
    require(len(subscriptions) == 3 and len(dns_profiles) == 2 and len(routes) == 3,
            "Desktop subscriptions, DNS profiles, or routing profiles were lost.")
    require(sum(bool(route["isActive"]) for route in routes) == 1,
            "The restored database does not have exactly one active routing profile.")
    require(any(group["isCurrent"] for group in groups), "The restored SubIndexId does not select a visible group.")

    all_profiles = authorized_json(host, token, "GET", "/api/profiles?subscriptionId=")
    templates: dict[str, dict] = {}
    for profile in all_profiles:
        protocol = str(profile.get("protocol", "")).lower()
        if profile.get("canTest") and protocol in ("vmess", "vless") and protocol not in templates:
            templates[protocol] = authorized_json(
                host, token, "GET", f"/api/profiles/{urllib.parse.quote(profile['indexId'])}")
    require("vless" in templates,
            "The Desktop fixture did not expose an ordinary VLESS profile for CRUD testing "
            f"(safe protocol summary: {sorted({str(item.get('protocol')) for item in all_profiles})}; "
            f"testable profiles: {sum(bool(item.get('canTest')) for item in all_profiles)}).")

    active_route = next(route for route in routes if route["isActive"])
    route_rules = authorized_json(host, token, "GET", f"/api/settings/routing-profiles/{active_route['id']}/rules")
    export_profile = next(profile for profile in all_profiles if profile.get("canTest"))
    exported = authorized_json(host, token, "POST", "/api/profiles/export", {
        "profileIds": [export_profile["indexId"]],
        "includeShareUris": False,
        "includeInnerUri": False,
        "includeClientConfig": True,
    })
    configs = [item["content"] for item in exported if item.get("format") == "client-config"]
    require(bool(configs), "Core config generation failed after Desktop restore.")
    generated_text = "\n".join(configs)
    require(any(value in generated_text for rule in route_rules for value in json.dumps(rule, ensure_ascii=False).split('"')
                if len(value) > 8 and value not in ("remarks", "outboundTag", "domain", "protocol")),
            "Generated client config did not include a value from the restored active routing rules.")

    db_path = next(host.data_home.rglob("guiNDB.db"))
    with sqlite3.connect(db_path) as database:
        subscription_columns = {row[1] for row in database.execute('PRAGMA table_info("SubItem")')}
        profile_columns = {row[1] for row in database.execute('PRAGMA table_info("ProfileItem")')}
        require("RequestHeaders" in subscription_columns and "EchForceQuery" in profile_columns,
                "ServiceLib schema upgrade dropped Desktop columns or did not add the Web nullable column.")

    # Create/edit/delete ordinary VMess and VLESS nodes with no group and with a real selected group.
    authorized_json(host, token, "PUT", "/api/profile-groups/current", {"subscriptionId": None})
    create_and_crud(host, token, new_vmess_profile(None), None)
    clone_and_crud(host, token, templates["vless"], None)
    group_id = subscriptions[0]["id"]
    authorized_json(host, token, "PUT", "/api/profile-groups/current", {"subscriptionId": group_id})
    create_and_crud(host, token, new_vmess_profile(group_id), group_id)

    vmess_payload = {
        "v": "2", "ps": f"web-restore-sub-{uuid.uuid4().hex[:8]}", "add": "example.invalid",
        "port": "443", "id": str(uuid.uuid4()), "aid": "0", "scy": "auto", "net": "tcp",
        "type": "none", "host": "", "path": "", "tls": "",
    }
    vmess_link = "vmess://" + base64.b64encode(json.dumps(vmess_payload).encode()).decode()
    import_result = host.request("POST", "/api/profiles/import", {
        "content": vmess_link,
        "subscriptionId": group_id,
        "isSubscription": True,
    }, token)[1]
    require(import_result.get("success") is True, f"Subscription-node import failed with code {import_result.get('code')}.")
    sub_profiles = authorized_json(host, token, "GET", f"/api/profiles?subscriptionId={urllib.parse.quote(group_id)}")
    imported = next(item for item in sub_profiles if item.get("remarks") == vmess_payload["ps"])
    imported_detail = authorized_json(host, token, "GET", f"/api/profiles/{urllib.parse.quote(imported['indexId'])}")
    imported_detail["remarks"] += "-edited"
    update = host.request("PUT", f"/api/profiles/{urllib.parse.quote(imported['indexId'])}", imported_detail, token)[1]
    require(update.get("success") is True, "Editing a subscription-imported node failed.")
    delete_status, delete_result = host.request("DELETE", "/api/profiles", {"profileIds": [imported["indexId"]]}, token)
    require(delete_status == 200 and delete_result.get("success") is True, "Deleting a subscription-imported node failed.")
    return export_profile["indexId"]


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: test-backup-fixtures.py Backup_Desktop.zip Backup_Web.zip", file=sys.stderr)
        return 2
    desktop_backup, web_backup = map(Path, sys.argv[1:])
    require(desktop_backup.is_file() and web_backup.is_file(), "Both real backup ZIPs must exist.")
    require(Path(DOTNET).is_file() or subprocess.call([DOTNET, "--version"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0,
            "The configured .NET SDK was not found.")

    with tempfile.TemporaryDirectory(prefix="v2rayn-backup-fixture-") as temp:
        root = Path(temp)
        data_home = root / "data"
        host = WebHost(data_home, free_port())
        try:
            host.start()
            setup_status, setup = host.request("POST", "/api/setup", {"key": API_KEY, "confirmKey": API_KEY})
            require(setup_status == 200 and setup.get("setupRequired") is False,
                    "Could not initialize the isolated fixture Web host.")
            token = setup["token"]
            auth_path = next(data_home.rglob("web-auth.json"))
            auth_bytes = auth_path.read_bytes()
            install_fake_cores(auth_path.parent.parent)

            backup_status, normal_backup = host.request("GET", "/api/backup/download", token=token)
            require(backup_status == 200 and isinstance(normal_backup, bytes), "Could not create an isolated Web backup.")
            with zipfile.ZipFile(__import__("io").BytesIO(normal_backup)) as archive:
                require(not any(name.lower().endswith("/web-auth.json") for name in archive.namelist()),
                        "A normal Web backup unexpectedly contains web-auth.json.")

            host.stop_current_gracefully()
            host.start(mode="background-child")
            token = host.login()
            wait_restart(host, desktop_backup, token)
            token = host.login()
            require(auth_path.read_bytes() == auth_bytes, "Desktop restore changed the Web-host management credential.")
            running_profile_id = exercise_desktop_restore(host, token)

            legacy_archive = root / "Backup_Desktop_legacy_routing.zip"
            migrated_route_id, isolated_proxy_port = make_legacy_desktop_routing_archive(desktop_backup, legacy_archive)
            wait_restart(host, legacy_archive, token)
            token = host.login()
            stopped_status = authorized_json(host, token, "GET", "/api/status")
            require(stopped_status["coreRunning"] is False and stopped_status["configuredProxyPort"] == isolated_proxy_port,
                    "The stopped runtime state or restored inbound port was not preserved.")
            routes = authorized_json(host, token, "GET", "/api/settings/routing-profiles")
            groups = authorized_json(host, token, "GET", "/api/profile-groups")
            require([route["id"] for route in routes if route["isActive"]] == [migrated_route_id],
                    "ServiceLib did not migrate legacy RoutingIndexId to the active RoutingItem.")
            require(any(group["isCurrent"] and group["id"] == "" for group in groups),
                    "A missing restored SubIndexId was not normalized to the all-profiles group.")
            config_path = next(data_home.rglob("guiNConfig.json"))
            restored_config = json.loads(config_path.read_text())
            routing_config = next(value for key, value in restored_config.items() if key.lower() == "routingbasicitem")
            require(not any(key.lower() == "routingindexid" and value for key, value in routing_config.items()),
                    "ServiceLib left the migrated legacy RoutingIndexId in guiNConfig.json.")

            start_status, start_result = host.request(
                "POST", f"/api/profiles/{urllib.parse.quote(running_profile_id)}/select", token=token)
            require(start_status == 200 and start_result.get("success") is True,
                    f"Could not start the fixture Core for restore-state coverage: {start_result.get('code')}.")
            running_status = authorized_json(host, token, "GET", "/api/status")
            require(running_status["coreRunning"] is True
                    and running_status["runningProfileId"] == running_profile_id
                    and running_status["runningProxyPort"] == isolated_proxy_port,
                    "Fixture Core did not run on the expected profile and real listener before restore.")

            lifecycle_log = auth_path.parent.parent / "core-lifecycle.log"
            lifecycle_before = lifecycle_log.read_text().splitlines()
            settings = authorized_json(host, token, "GET", "/api/settings")

            # The projected listener check must reject a conflicting port while the
            # currently healthy Core still owns its previous listener.
            conflict_listener = socket.socket()
            conflict_listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            conflict_listener.bind(("127.0.0.1", 0))
            conflict_listener.listen(8)
            try:
                conflicting_settings = settings_apply_body(settings)
                conflicting_settings["inbound"]["localPort"] = conflict_listener.getsockname()[1]
                conflict_status, conflict_result = host.request("PUT", "/api/settings/apply", conflicting_settings, token)
                conflict_data = conflict_result.get("data", {})
                after_conflict = authorized_json(host, token, "GET", "/api/status")
                require(conflict_status == 400 and conflict_result.get("success") is False
                        and conflict_data.get("coreResultCode") == "proxy_port_in_use"
                        and conflict_data.get("rolledBack") is True
                        and conflict_data.get("configRestored") is True
                        and conflict_data.get("configSaved") is False
                        and conflict_data.get("oldRuntimeRestored") is True
                        and after_conflict["coreRunning"] is True
                        and after_conflict["coreProcessIds"] == running_status["coreProcessIds"]
                        and after_conflict["runningProxyPort"] == running_status["runningProxyPort"]
                        and after_conflict["configuredProxyPort"] == running_status["configuredProxyPort"]
                        and lifecycle_log.read_text().splitlines() == lifecycle_before,
                        "An occupied projected listener caused an unnecessary Core outage or left Config changed.")
            finally:
                conflict_listener.close()

            udp_conflict_listener = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            udp_conflict_listener.bind(("127.0.0.1", 0))
            try:
                udp_conflicting_settings = settings_apply_body(settings)
                udp_conflicting_settings["inbound"].update({
                    "localPort": udp_conflict_listener.getsockname()[1],
                    "udpEnabled": True,
                    "secondLocalPortEnabled": False,
                    "allowLANConn": False,
                    "newPort4LAN": False,
                })
                udp_conflict_status, udp_conflict_result = host.request(
                    "PUT", "/api/settings/apply", udp_conflicting_settings, token)
                udp_conflict_data = udp_conflict_result.get("data", {})
                after_udp_conflict = authorized_json(host, token, "GET", "/api/status")
                require(udp_conflict_status == 400 and udp_conflict_result.get("success") is False
                        and udp_conflict_data.get("coreResultCode") == "proxy_port_in_use"
                        and udp_conflict_data.get("rolledBack") is True
                        and after_udp_conflict["coreRunning"] is True
                        and after_udp_conflict["coreProcessIds"] == running_status["coreProcessIds"]
                        and lifecycle_log.read_text().splitlines() == lifecycle_before,
                        "An occupied UDP inbound port was not rejected before stopping the old Core.")
            finally:
                udp_conflict_listener.close()

            no_change_result = apply_all_settings(host, token, settings_apply_body(settings))
            no_change_status = authorized_json(host, token, "GET", "/api/status")
            require(no_change_result.get("data", {}).get("coreRestarted") is False
                    and no_change_status["coreProcessIds"] == running_status["coreProcessIds"]
                    and lifecycle_log.read_text().splitlines() == lifecycle_before,
                    "Saving unchanged settings restarted the running Core "
                    f"(changed={no_change_result.get('data', {}).get('changed')}, "
                    f"coreRestarted={no_change_result.get('data', {}).get('coreRestarted')}, "
                    f"pidSame={no_change_status['coreProcessIds'] == running_status['coreProcessIds']}).")

            previous_process_ids = no_change_status["coreProcessIds"]
            changed_port = free_port()
            changed_settings = settings_apply_body(settings)
            changed_settings["inbound"]["localPort"] = changed_port
            changed_settings["core"]["logEnabled"] = not changed_settings["core"]["logEnabled"]
            strategies = settings["options"]["routingBasicDomainStrategies"]
            changed_settings["domainStrategy"] = next(
                strategy for strategy in strategies if strategy != settings["domainStrategy"])
            apply_result = apply_all_settings(host, token, changed_settings)
            reapplied_status = authorized_json(host, token, "GET", "/api/status")
            lifecycle_after = lifecycle_log.read_text().splitlines()
            new_lifecycle = lifecycle_after[len(lifecycle_before):]
            require(apply_result.get("messageKey") == "core.restarted"
                    and reapplied_status["coreRunning"] is True
                    and reapplied_status["configuredProxyPort"] == changed_port
                    and reapplied_status["runningProxyPort"] == changed_port
                    and reapplied_status["coreProcessIds"] != previous_process_ids
                    and sum(line.startswith("stop ") for line in new_lifecycle) == 1
                    and sum(line.startswith("start:") for line in new_lifecycle) == 1,
                    "All-settings apply did not perform exactly one restart on the new actual listener port.")

            # Simulate a serving Core that loses its listener but remains alive. Web must
            # detect the fault, then use its tracked child PID and upstream CoreStop API
            # to stop and restart it when applying Core-affecting settings.
            drop_listener_file = auth_path.parent.parent / "fake-core-drop-listener"
            drop_listener_file.write_text("drop the listener but keep the process alive")
            faulted_status = None
            deadline = time.monotonic() + 8
            while time.monotonic() < deadline:
                faulted_status = authorized_json(host, token, "GET", "/api/status")
                if faulted_status["runtimeState"] == "faulted":
                    break
                time.sleep(0.1)
            drop_listener_file.unlink(missing_ok=True)
            require(faulted_status is not None
                    and faulted_status["runtimeState"] == "faulted"
                    and len(faulted_status["coreProcessIds"]) > 0,
                    "The fake Core did not enter the Faulted-with-active-child regression state.")
            recovered_port = free_port()
            recovery_settings = settings_apply_body(authorized_json(host, token, "GET", "/api/settings"))
            recovery_settings["inbound"]["localPort"] = recovered_port
            recovery_result = apply_all_settings(host, token, recovery_settings)
            recovered_status = authorized_json(host, token, "GET", "/api/status")
            require(recovery_result.get("data", {}).get("coreRestarted") is True
                    and recovered_status["runtimeState"] == "running"
                    and recovered_status["coreRunning"] is True
                    and recovered_status["configuredProxyPort"] == recovered_port
                    and recovered_status["runningProxyPort"] == recovered_port,
                    "Faulted Core with a live child was reported as applied without a controlled recovery restart.")

            # Active routing rows are part of the generated Core configuration too.
            # A failed restart must restore the old SQLite routing snapshot, not only Config JSON.
            route_profiles = authorized_json(host, token, "GET", "/api/settings/routing-profiles")
            active_route = next(route for route in route_profiles if route["isActive"])
            route_path = f"/api/settings/routing-profiles/{urllib.parse.quote(active_route['id'])}/rules"
            old_active_rules = authorized_json(host, token, "GET", route_path)
            fail_once_path = auth_path.parent.parent / "fake-core-fail-once-port"
            fail_once_path.write_text(str(recovered_port))
            route_status, route_apply = host.request("POST", route_path + "/import", {
                "content": json.dumps([{
                    "id": "fixture-route-rollback",
                    "outboundTag": "direct",
                    "domain": ["domain:example.invalid"],
                    "enabled": True,
                    "remarks": "rollback regression",
                }]),
                "append": False,
            }, token)
            after_route_rollback = authorized_json(host, token, "GET", route_path)
            route_rollback_status = authorized_json(host, token, "GET", "/api/status")
            require(route_status == 400 and route_apply.get("success") is False
                    and route_apply.get("data", {}).get("rolledBack") is True
                    and route_apply.get("data", {}).get("configRestored") is True
                    and route_apply.get("data", {}).get("oldRuntimeRestored") is True
                    and after_route_rollback == old_active_rules
                    and route_rollback_status["coreRunning"] is True
                    and route_rollback_status["runningProxyPort"] == recovered_port,
                    "A failed active-routing restart did not restore both SQLite rows and the old Core runtime.")

            # If the newly generated Core exits after old Core shutdown, the settings
            # transaction must persist old Config and return the old proxy runtime.
            failed_new_port = free_port()
            fail_once_path.write_text(str(failed_new_port))
            failed_launch_settings = settings_apply_body(authorized_json(host, token, "GET", "/api/settings"))
            failed_launch_settings["inbound"]["localPort"] = failed_new_port
            failed_launch_status, failed_launch = host.request(
                "PUT", "/api/settings/apply", failed_launch_settings, token)
            rolled_back_status = authorized_json(host, token, "GET", "/api/status")
            rollback_data = failed_launch.get("data", {})
            require(failed_launch_status == 400 and failed_launch.get("success") is False
                    and rollback_data.get("coreResultCode") == "core_start_failed"
                    and rollback_data.get("rolledBack") is True
                    and rollback_data.get("configRestored") is True
                    and rollback_data.get("oldRuntimeRestored") is True
                    and rolled_back_status["coreRunning"] is True
                    and rolled_back_status["runtimeState"] == "running"
                    and rolled_back_status["configuredProxyPort"] == recovered_port
                    and rolled_back_status["runningProxyPort"] == recovered_port,
                    "A failed new Core launch did not restore the previous Config and working proxy runtime.")

            # A failed rollback is not hidden: keep the old Config, expose Faulted state,
            # and preserve both the original apply error and rollback diagnostics.
            fail_ports_path = auth_path.parent.parent / "fake-core-fail-ports"
            failed_new_port = free_port()
            fail_ports_path.write_text(f"{failed_new_port}\n{recovered_port}\n")
            rollback_failure_settings = settings_apply_body(authorized_json(host, token, "GET", "/api/settings"))
            rollback_failure_settings["inbound"]["localPort"] = failed_new_port
            rollback_failure_status, rollback_failure = host.request(
                "PUT", "/api/settings/apply", rollback_failure_settings, token)
            faulted_rollback_status = authorized_json(host, token, "GET", "/api/status")
            rollback_failure_data = rollback_failure.get("data", {})
            require(rollback_failure_status == 400 and rollback_failure.get("success") is False
                    and rollback_failure_data.get("rolledBack") is False
                    and rollback_failure_data.get("configRestored") is True
                    and rollback_failure_data.get("oldRuntimeRestored") is False
                    and rollback_failure_data.get("rollbackDetail")
                    and faulted_rollback_status["runtimeState"] == "faulted"
                    and faulted_rollback_status["configuredProxyPort"] == recovered_port,
                    "A failed runtime rollback was not reported with explicit Faulted diagnostics.")
            fail_ports_path.unlink(missing_ok=True)
            start_after_rollback, start_after_rollback_result = host.request("POST", "/api/core/start", token=token)
            require(start_after_rollback == 200 and start_after_rollback_result.get("success") is True,
                    "The fixture Core could not recover after the rollback-failure diagnostic case.")

            stop_status, stop_result = host.request("POST", "/api/core/stop", token=token)
            require(stop_status == 200 and stop_result.get("success") is True,
                    "Core could not be stopped before the stopped-settings apply case.")
            stopped_port = free_port()
            stopped_settings = settings_apply_body(authorized_json(host, token, "GET", "/api/settings"))
            stopped_settings["inbound"]["localPort"] = stopped_port
            stopped_lifecycle_before = lifecycle_log.read_text().splitlines()
            apply_all_settings(host, token, stopped_settings)
            stopped_after_settings = authorized_json(host, token, "GET", "/api/status")
            require(stopped_after_settings["coreRunning"] is False
                    and stopped_after_settings["runtimeState"] == "stopped"
                    and stopped_after_settings["configuredProxyPort"] == stopped_port
                    and lifecycle_log.read_text().splitlines() == stopped_lifecycle_before,
                    "Saving Core settings incorrectly started a previously stopped Core.")

            start_status, start_result = host.request(
                "POST", f"/api/profiles/{urllib.parse.quote(running_profile_id)}/select", token=token)
            require(start_status == 200 and start_result.get("success") is True,
                    "Core could not restart on the second isolated listener port.")
            running_again = authorized_json(host, token, "GET", "/api/status")
            require(running_again["coreRunning"] is True and running_again["runningProxyPort"] == stopped_port,
                    "Runtime snapshot did not report the second actual Core port.")

            wait_restart(host, legacy_archive, token)
            token = host.login()
            restored_running_status = authorized_json(host, token, "GET", "/api/status")
            require(restored_running_status["coreRunning"] is True
                    and restored_running_status["runningProfileId"] == running_profile_id
                    and restored_running_status["runningProxyPort"] == isolated_proxy_port,
                    "A Core that was running before Desktop restore was not resumed with its original profile and port.")

            stop_status, stop_result = host.request("POST", "/api/core/stop", token=token)
            require(stop_status == 200 and stop_result.get("success") is True,
                    "Core could not be stopped before the stopped-state Web restore case.")
            malicious_web = root / "Backup_Web_with_malicious_auth.zip"
            add_auth_file_to_web_archive(web_backup, malicious_web)
            wait_restart(host, malicious_web, token)
            token = host.login()
            require(auth_path.read_bytes() == auth_bytes, "Web restore replaced the local Web-host management credential.")
            restored_profiles = authorized_json(host, token, "GET", "/api/profiles?subscriptionId=")
            restored_subscriptions = authorized_json(host, token, "GET", "/api/subscriptions")
            require(len(restored_profiles) == 36 and len(restored_subscriptions) == 2,
                    "The real Web backup was not restored after the auth-file filtering test.")
            print("PASS: real Desktop/Web restore, native auto-restart/--stop, unchanged and multi-section settings apply, actual port restart, running/stopped Core restoration, ServiceLib routing migration, active-route config generation, auth isolation, and profile CRUD.")
        finally:
            host.close()
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except FixtureFailure as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
