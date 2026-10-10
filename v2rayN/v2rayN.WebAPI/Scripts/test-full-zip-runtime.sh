#!/usr/bin/env bash
set -euo pipefail

archive="${1:?Usage: test-full-zip-runtime.sh <v2rayN-linux-64-WebAPI.zip>}"
archive="$(realpath "$archive")"
test -s "$archive"
command -v curl >/dev/null
command -v python3 >/dev/null
command -v unzip >/dev/null

temporary="$(mktemp -d)"
web_pid=""
cleanup() {
  if [[ -n "$web_pid" ]] && kill -0 "$web_pid" 2>/dev/null; then
    kill -TERM "$web_pid" 2>/dev/null || true
    for _ in {1..20}; do
      state="$(ps -o stat= -p "$web_pid" 2>/dev/null | tr -d ' ' || true)"
      [[ -z "$state" || "$state" == *Z* ]] && break
      sleep 0.1
    done
    kill -KILL "$web_pid" 2>/dev/null || true
    wait "$web_pid" 2>/dev/null || true
  fi
  rm -rf "$temporary"
}
trap cleanup EXIT HUP INT TERM

install_dir="$temporary/install"
data_home="$temporary/data"
mkdir -p "$install_dir" "$data_home" "$temporary/dotnet-bundle" "$temporary/other-working-directory"
unzip -q "$archive" -d "$install_dir"
executable="$install_dir/v2rayN.WebAPI"
test -x "$executable"
test -s "$install_dir/.env.example"
expected_version="$(python3 - "$archive" <<'PY'
import json
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1]) as package:
    identity = json.loads(package.read("v2rayN.WebAPI.build.json"))
assert identity["product"] == "v2rayN.WebAPI", identity
assert identity["rid"] == "linux-x64", identity
print(identity["version"])
PY
)"

port="$(python3 - <<'PY'
import socket

with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
)"
management_key="web-zip-smoke-$(python3 -c 'import secrets; print(secrets.token_hex(24))')"
cp "$install_dir/.env.example" "$install_dir/.env"
python3 - "$install_dir/.env" "$management_key" "$port" <<'PY'
import sys
from pathlib import Path

path, key, port = sys.argv[1:]
content = Path(path).read_text(encoding="utf-8")
replacements = {
    "V2RAYN_WEB_API_KEY=": f"V2RAYN_WEB_API_KEY={key}",
    "ASPNETCORE_URLS=http://127.0.0.1:5080": f"ASPNETCORE_URLS=http://127.0.0.1:{port}",
    "V2RAYN_WEB_AUTOSTART=true": "V2RAYN_WEB_AUTOSTART=false",
}
for original, replacement in replacements.items():
    assert content.count(original) == 1, original
    content = content.replace(original, replacement, 1)
Path(path).write_text(content, encoding="utf-8")
PY
chmod 600 "$install_dir/.env"

(
  cd "$temporary/other-working-directory"
  exec env \
    -u V2RAYN_WEB_API_KEY \
    -u V2RAYN_WEB_AUTOSTART \
    -u ASPNETCORE_URLS \
    -u ASPNETCORE_HTTP_PORTS \
    V2RAYN_DATA_HOME="$data_home" \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR="$temporary/dotnet-bundle" \
    "$executable" --foreground --no-open
) >"$temporary/web.log" 2>&1 &
web_pid=$!
health_url="http://127.0.0.1:$port/api/health"
healthy=false
for _ in {1..90}; do
  if ! kill -0 "$web_pid" 2>/dev/null; then
    cat "$temporary/web.log" >&2
    echo "Web process exited before /api/health became ready." >&2
    exit 1
  fi
  if curl --noproxy '*' --silent --show-error --fail "$health_url" \
      -D "$temporary/health.headers" -o "$temporary/health.json" 2>/dev/null \
      && python3 - "$temporary/health.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
assert response == {"status": "ok"}, response
PY
  then
    healthy=true
    break
  fi
  sleep 1
done
if [[ "$healthy" != true ]]; then
  cat "$temporary/web.log" >&2
  echo "Full ZIP did not become healthy within 90 seconds." >&2
  exit 1
fi
test "$(curl --noproxy '*' --silent --show-error -o /dev/null -w '%{http_code}' "http://127.0.0.1:$port/")" = 404
grep -Fiq 'X-v2rayn-WebAPI-instance-pid:' "$temporary/health.headers"
grep -Fiq "X-v2rayn-WebAPI-version: $expected_version" "$temporary/health.headers"
grep -Fiq 'X-v2rayn-WebAPI-core-state: stopped' "$temporary/health.headers"
grep -Fiq 'X-v2rayn-WebAPI-core-process-ids:' "$temporary/health.headers"

login_body="$(python3 -c 'import json,sys; print(json.dumps({"key": sys.argv[1]}))' "$management_key")"
curl --noproxy '*' --silent --show-error --fail \
  -H 'Content-Type: application/json' \
  --data "$login_body" \
  "http://127.0.0.1:$port/api/auth/login" >"$temporary/login.json"
session_token="$(python3 - "$temporary/login.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
assert response["success"] is True, response
print(response["data"]["token"])
PY
)"
curl --noproxy '*' --silent --show-error --fail \
  -H "Authorization: Bearer $session_token" \
  "http://127.0.0.1:$port/api/status" >"$temporary/status.json"
python3 - "$temporary/status.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
status = response["data"]
assert response["success"] is True, response
assert status["coreRunning"] is False, status
assert status["runtimeState"] == "stopped", status
assert not status["coreProcessIds"], status
PY

kill -TERM "$web_pid"
stopped=false
for _ in {1..120}; do
  state="$(ps -o stat= -p "$web_pid" 2>/dev/null | tr -d ' ' || true)"
  if [[ -z "$state" || "$state" == *Z* ]]; then
    stopped=true
    break
  fi
  sleep 0.25
done
if [[ "$stopped" != true ]]; then
  echo "Web process did not shut down after SIGTERM." >&2
  cat "$temporary/web.log" >&2
  exit 1
fi
set +e
wait "$web_pid"
exit_code=$?
set -e
web_pid=""
if [[ "$exit_code" -ne 0 ]]; then
  echo "Web process exited with status $exit_code during graceful shutdown." >&2
  cat "$temporary/web.log" >&2
  exit 1
fi

python3 - "$install_dir" "$data_home" <<'PY'
import os
import sys

install, data_home = map(os.path.realpath, sys.argv[1:])
expected = {
    os.path.join(install, "v2rayN.WebAPI"),
    os.path.join(install, "bin", "xray", "xray"),
    os.path.join(install, "bin", "sing_box", "sing-box"),
    os.path.join(install, "bin", "mihomo", "mihomo"),
    os.path.join(data_home, "v2rayN", "bin", "xray", "xray"),
    os.path.join(data_home, "v2rayN", "bin", "sing_box", "sing-box"),
    os.path.join(data_home, "v2rayN", "bin", "mihomo", "mihomo"),
}
expected = {os.path.realpath(path) for path in expected if os.path.exists(path)}
residual = []
for entry in os.scandir("/proc"):
    if not entry.name.isdigit():
        continue
    try:
        executable = os.path.realpath(f"/proc/{entry.name}/exe")
    except (OSError, PermissionError):
        continue
    if executable in expected:
        residual.append((entry.name, executable))
assert not residual, f"Residual Web/Core processes remain: {residual}"
PY

echo "Full ZIP runtime smoke passed (healthy API, authenticated stopped Core, graceful shutdown, no residual Web/Core processes)."
