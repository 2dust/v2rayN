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
        if headers.get("X-v2rayn-web-version") == version:
            return headers
        time.sleep(0.1)
    raise AssertionError("native instance failed its version health check")


def run_scenario(root, previous, candidate, rollback, reject_invalid=False):
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
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    url = f"http://127.0.0.1:{port}"
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
        staged = root / (".v2rayn-web-candidate-" + secrets.token_hex(8))
        backup = root / (".v2rayn-web-backup-" + secrets.token_hex(8))
        staged.mkdir()
        (staged / (NAME + ".build.json")).write_text(json.dumps(after))
        shutil.copy2(candidate / NAME, staged / NAME)
        if reject_invalid:
            (staged / NAME).write_text("#!/bin/sh\nexit 1\n")
        intent = temps / "web-update-runtime-state.json"
        progress = temps / "web-update-progress.json"
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
        helper = subprocess.Popen([str(install / NAME), "--apply-web-update", str(plan_path)], env=env, stdout=log, stderr=log)
        if not reject_invalid:
            owner.send_signal(signal.SIGTERM)
            owner.wait(timeout=40)
        helper_code = helper.wait(timeout=100)
        assert not list(install.glob(".v2rayn-web-update-helper-*")), "Isolated helper bundle was not cleaned after worker exit"
        failed = rollback or reject_invalid
        assert helper_code == (1 if failed else 0), f"helper returned {helper_code}; inspect {root / 'runtime.log'} and {progress}"
        wait_health(url, before["version"] if failed else after["version"])
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
        print("Native WebAPI " + label + " passed (version/PID/lock health, atomic executable swap, unrelated file/Core preservation).")
    finally:
        if owner.poll() is None:
            owner.send_signal(signal.SIGTERM)
            owner.wait(timeout=40)
        if health(url):
            subprocess.run([str(install / NAME), "--stop", "--urls", url], env=env, stdout=log, stderr=log, check=True, timeout=40)
        log.close()


if __name__ == "__main__":
    previous, candidate = [Path(value).resolve() for value in sys.argv[1:]]
    root = Path(tempfile.mkdtemp(prefix="webapi-native-identity-", dir=os.environ.get("TMPDIR", "/tmp/opencode")))
    try:
        run_scenario(root / "replacement", previous, candidate, False)
        run_scenario(root / "rollback", previous, candidate, True)
        run_scenario(root / "invalid-executable", previous, candidate, False, True)
    except Exception:
        print(f"Failed fixture retained for diagnosis: {root}", file=sys.stderr)
        raise
    else:
        shutil.rmtree(root)
