#!/usr/bin/env python3
"""Real native WebAPI helper replacement/rollback smoke; uses isolated data only.

Usage: test-native-update-identity.py <previous-publish-dir> <candidate-publish-dir>
The two artifacts must carry distinct versions in v2rayN.WebAPI.build.json.
"""
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import signal
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

NAME = "v2rayN.WebAPI"
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def fingerprint(directory):
    return {str(p.relative_to(directory)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in directory.rglob("*") if p.is_file()}


def health(url):
    try:
        with opener.open(url + "/api/health", timeout=1) as response:
            return dict(response.headers)
    except (OSError, urllib.error.URLError):
        return {}


def wait_health(url, version):
    for _ in range(300):
        headers = health(url)
        if headers.get("X-v2rayn-WebAPI-version") == version:
            return headers
        time.sleep(0.1)
    raise AssertionError("native instance failed its version health check")


def run_scenario(root, previous, candidate, rollback, reject_invalid=False, listen_host="127.0.0.1"):
    root.mkdir()
    install = root / "install"
    install.mkdir()
    shutil.copy2(previous / NAME, install / NAME)
    shutil.copy2(previous / (NAME + ".build.json"), install / (NAME + ".build.json"))
    before = json.loads((previous / (NAME + ".build.json")).read_text())
    after = json.loads((candidate / (NAME + ".build.json")).read_text())
    assert before["product"] == after["product"] == NAME
    assert before["version"] != after["version"]
    if rollback:
        # Keep a real native executable. Force a version-health mismatch rather
        # than bypassing package validation with a synthetic shell executable.
        after["version"] = "0.0.999-review"
    data = root / "data"
    scope = data / "v2rayN"
    temps = scope / "guiTemps"
    temps.mkdir(parents=True)
    (install / "extra").mkdir()
    (install / "extra" / "marker.dat").write_text("unrelated files must survive")
    (scope / "bin").mkdir()
    (scope / "bin" / "marker").write_text("Core files must survive")
    (install / "bin").mkdir()
    (install / "bin" / ".keep").touch()  # Full-install package requires a bin/ root.
    family = socket.AF_INET6 if ":" in listen_host else socket.AF_INET
    with socket.socket(family) as sock:
        sock.bind((listen_host, 0))
        port = sock.getsockname()[1]
    url = f"http://[{listen_host}]:{port}" if ":" in listen_host else f"http://{listen_host}:{port}"
    env = {**os.environ, "V2RAYN_WEB_API_KEY": secrets.token_urlsafe(32),
           "V2RAYN_DATA_HOME": str(data), "XDG_DATA_HOME": str(data),
           "V2RAYN_LOCAL_APPLICATION_DATA_V2": "1", "ASPNETCORE_URLS": url,
           "V2RAYN_WEB_AUTOSTART": "false",
           "DOTNET_BUNDLE_EXTRACT_BASE_DIR": str(root / ".net-bundle")}
    for key in ("INVOCATION_ID", "JOURNAL_STREAM", "container", "DOTNET_RUNNING_IN_CONTAINER"):
        env.pop(key, None)
    log = (root / "runtime.log").open("wb")
    owner = subprocess.Popen([str(install / NAME), "--foreground", "--no-open"], env=env, stdout=log, stderr=log)
    try:
        wait_health(url, before["version"])
        preserved = {str(p): fingerprint(p) for p in (install / "extra", install / "bin", scope / "bin")}
        staged = root / (".v2rayn-WebAPI-candidate-" + secrets.token_hex(8))
        backup = root / (".v2rayn-WebAPI-backup-" + secrets.token_hex(8))
        staged.mkdir()
        (staged / (NAME + ".build.json")).write_text(json.dumps(after))
        shutil.copy2(candidate / NAME, staged / NAME)
        if reject_invalid:
            (staged / NAME).write_text("#!/bin/sh\nexit 1\n")
        intent = temps / "WebAPI-update-runtime-state.json"
        progress = temps / "WebAPI-update-progress.json"
        intent.write_text(json.dumps({"wasRunning": False, "profileId": None}))
        plan_path = temps / "identity-update-plan.json"
        plan_path.write_text(json.dumps({
            "installDirectory": str(install), "candidateDirectory": str(staged), "backupDirectory": str(backup),
            "instanceLockPath": str(scope / (NAME + ".instance.lock")), "healthUri": url + "/api/health",
            "hostArguments": ["--urls", url], "expectedVersion": after["version"], "expectedCommit": after["commit"],
            "rid": after["rid"], "previousVersion": before["version"],
            "runtimeIntentPath": str(intent), "progressPath": str(progress), "coreWasRunning": False,
        }))
        plan_path.chmod(0o600)
        if reject_invalid:
            owner.send_signal(signal.SIGTERM)
            owner.wait(timeout=40)
        helper = subprocess.Popen([str(install / NAME), "--apply-WebAPI-update", str(plan_path)], env=env, stdout=log, stderr=log)
        if not reject_invalid:
            owner.send_signal(signal.SIGTERM)
            owner.wait(timeout=40)
        helper_code = helper.wait(timeout=100)
        assert not list(install.glob(".v2rayn-WebAPI-update-helper-*")), "Isolated helper bundle was not cleaned after worker exit"
        failed = rollback or reject_invalid
        expected_helper_code = 1 if failed else 0
        if helper_code != expected_helper_code:
            log.flush()
            runtime_log = (root / "runtime.log").read_text(errors="replace")
            progress_state = progress.read_text(errors="replace") if progress.exists() else "<missing>"
            raise AssertionError(
                f"helper returned {helper_code}, expected {expected_helper_code};\n"
                f"runtime.log:\n{runtime_log}\nupdate progress:\n{progress_state}"
            )
        wait_health(url, before["version"] if failed else after["version"])
        if failed:
            # The old build must be restored and running, not merely copied back:
            # health was already polled above and must come from a replacement process.
            running = health(url)
            assert int(running["X-v2rayn-WebAPI-instance-pid"]) != owner.pid, \
                "the restored build must be a running replacement process"
        result = json.loads(progress.read_text())
        assert result["isComplete"] and result["success"] == (not failed)
        if failed:
            assert result["rollbackSucceeded"]
        expected = previous if failed else candidate
        assert (install / NAME).read_bytes() == (expected / NAME).read_bytes()
        assert not (install / "v2rayN.Web").exists()
        for directory, snapshot in preserved.items():
            assert fingerprint(Path(directory)) == snapshot
        assert not backup.exists() and not intent.exists()
        label = "invalid executable rejection" if reject_invalid else "rollback" if rollback else "replacement"
        suffix = "" if listen_host == "127.0.0.1" else f" ({listen_host})"
        print("Native WebAPI " + label + suffix + " passed (version/PID/lock health, atomic executable swap, unrelated file/Core preservation).")
    finally:
        if owner.poll() is None:
            owner.send_signal(signal.SIGTERM)
            owner.wait(timeout=40)
        if health(url):
            subprocess.run([str(install / NAME), "--stop", "--urls", url], env=env, stdout=log, stderr=log, check=True, timeout=40)
        log.close()


def primary_ipv4():
    # Documentation-range UDP connect selects the primary interface without
    # sending any packets; UDP "connect" only resolves the local address.
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.connect(("192.0.2.1", 9))
            address = sock.getsockname()[0]
        return None if address.startswith("127.") else address
    except OSError:
        return None


def ipv6_loopback_available():
    if not socket.has_ipv6:
        return False
    try:
        with socket.socket(socket.AF_INET6) as sock:
            sock.bind(("::1", 0))
        return True
    except OSError:
        return False


def run_refusal_scenario(root, previous):
    """An instance whose listeners have no loopback HTTP probe must refuse self-updates
    and keep serving instead of stopping itself for an unverifiable replacement."""
    host = primary_ipv4()
    if host is None:
        print("Skipped the update-refusal scenario: no non-loopback IPv4 address is available.")
        return
    root.mkdir()
    install = root / "install"
    install.mkdir()
    shutil.copy2(previous / NAME, install / NAME)
    shutil.copy2(previous / (NAME + ".build.json"), install / (NAME + ".build.json"))
    data = root / "data"
    scope = data / "v2rayN"
    temps = scope / "guiTemps"
    temps.mkdir(parents=True)
    (install / "bin").mkdir()
    (install / "bin" / ".keep").touch()
    with socket.socket() as sock:
        sock.bind((host, 0))
        port = sock.getsockname()[1]
    url = f"http://{host}:{port}"
    key = secrets.token_urlsafe(32)
    env = {**os.environ, "V2RAYN_WEB_API_KEY": key, "V2RAYN_DATA_HOME": str(data), "XDG_DATA_HOME": str(data),
           "V2RAYN_LOCAL_APPLICATION_DATA_V2": "1", "ASPNETCORE_URLS": url,
           "V2RAYN_WEB_AUTOSTART": "false",
           "DOTNET_BUNDLE_EXTRACT_BASE_DIR": str(root / ".net-bundle")}
    for entry in ("INVOCATION_ID", "JOURNAL_STREAM", "container", "DOTNET_RUNNING_IN_CONTAINER"):
        env.pop(entry, None)
    log = (root / "runtime.log").open("wb")
    owner = subprocess.Popen([str(install / NAME), "--foreground", "--no-open"], env=env, stdout=log, stderr=log)
    try:
        # The health diagnostics are loopback-only, so a non-loopback listener only
        # guarantees a successful response body here.
        for _ in range(300):
            if health(url):
                break
            time.sleep(0.1)
        else:
            raise AssertionError("the non-loopback instance did not become ready")

        def login():
            request = urllib.request.Request(
                url + "/api/auth/login", method="POST",
                data=json.dumps({"key": key}).encode(),
                headers={"Content-Type": "application/json"})
            with opener.open(request, timeout=5) as response:
                body = json.load(response)
            assert body["success"] is True, body
            return body["data"]["token"]

        token = login()

        def api(path, method="GET", expect=200):
            request = urllib.request.Request(
                url + path, method=method,
                headers={"Authorization": f"Bearer {token}"})
            try:
                with opener.open(request, timeout=10) as response:
                    status = response.status
                    body = json.load(response)
            except urllib.error.HTTPError as error:
                status = error.code
                with error:
                    body = json.load(error)
            assert status == expect, (status, body)
            return body

        target = api("/api/web-updates")
        assert target["success"] is True, target
        view = target["data"]
        assert view["canInstall"] is False, view
        assert view["installReasonKey"] == "maintenance.webUpdateProbeUnavailable", view

        # A refused install answers 409 Conflict with the reason view.
        refusal = api("/api/web-updates/update", method="POST", expect=409)
        assert refusal["success"] is False, refusal
        assert refusal["code"] == "web_update_runtime_unavailable", refusal
        assert refusal["messageKey"] == "maintenance.webUpdateProbeUnavailable", refusal

        # The same unprobeable configuration must also make --stop refuse to signal
        # instead of guessing a loopback address for owner verification.
        stop = subprocess.run(
            [str(install / NAME), "--stop", "--urls", url],
            env={**env, "LC_ALL": "C", "LANG": "C"},
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=40)
        assert stop.returncode != 0, stop.stdout
        assert b"loopback HTTP health endpoint" in stop.stdout, stop.stdout
        assert owner.poll() is None, "an unprobeable --stop must not stop the current instance"

        assert owner.poll() is None, "the refused update must not stop the current instance"
        assert health(url), "the current instance must keep serving health after the refusal"
        lock_path = scope / (NAME + ".instance.lock")
        assert lock_path.exists() and int(lock_path.read_text().strip()) == owner.pid, \
            "the refused update must keep the same lock owner"
        assert not (temps / "WebAPI-update-progress.json").exists(), \
            "a refused update must not start publishing update progress"
        assert not list(root.glob(".v2rayn-WebAPI-candidate-*")), "no update candidate may be staged"
        assert not list(install.parent.glob(".v2rayn-WebAPI-update-helper-*")), "no update helper may be started"
        print("Native WebAPI unprobeable-listener refusal passed (canInstall=false with a clear reason, current instance keeps running).")
    finally:
        if owner.poll() is None:
            owner.send_signal(signal.SIGTERM)
            owner.wait(timeout=40)
        log.close()


if __name__ == "__main__":
    previous, candidate = [Path(value).resolve() for value in sys.argv[1:]]
    root = Path(tempfile.mkdtemp(prefix="webapi-native-identity-", dir=os.environ.get("TMPDIR", "/tmp/opencode")))
    try:
        run_scenario(root / "replacement", previous, candidate, False)
        run_scenario(root / "rollback", previous, candidate, True)
        run_scenario(root / "invalid-executable", previous, candidate, False, True)
        if ipv6_loopback_available():
            run_scenario(root / "ipv6-only-replacement", previous, candidate, False, listen_host="::1")
        else:
            print("Skipped the IPv6-only update scenario: IPv6 loopback is not available.")
        run_refusal_scenario(root / "probe-refused", previous)
    except Exception:
        print(f"Failed fixture retained for diagnosis: {root}", file=sys.stderr)
        raise
    else:
        shutil.rmtree(root)
