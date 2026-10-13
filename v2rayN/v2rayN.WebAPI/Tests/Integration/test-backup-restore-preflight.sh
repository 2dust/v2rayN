#!/usr/bin/env bash
set -euo pipefail

executable="$(realpath "${1:?Usage: test-backup-restore-preflight.sh <native-v2rayN.WebAPI-executable>}")"
script_path="$(realpath "${BASH_SOURCE[0]}")"
test -x "$executable"
command -v curl >/dev/null
command -v python3 >/dev/null
command -v openssl >/dev/null

# A service-based Linux runner may keep this test in system.slice even after its
# environment markers are cleared. Move the entire test (and the WebAPI children
# it starts) into a transient scope so production supervisor detection stays intact.
if [[ "${V2RAYN_RESTORE_PREFLIGHT_TEST_SCOPE:-}" != "1" ]]; then
  scope_environment=(V2RAYN_RESTORE_PREFLIGHT_TEST_SCOPE=1)
  if [[ -n "${TMPDIR:-}" ]]; then scope_environment+=("TMPDIR=$TMPDIR"); fi
  if command -v systemd-run >/dev/null && systemd-run --user --scope --quiet /usr/bin/true >/dev/null 2>&1; then
    exec systemd-run --user --scope --quiet /usr/bin/env \
      "${scope_environment[@]}" /usr/bin/bash "$script_path" "$executable"
  elif command -v sudo >/dev/null && sudo -n systemd-run --scope --quiet /usr/bin/true >/dev/null 2>&1; then
    runuser_bin="$(command -v runuser)"
    exec sudo -n systemd-run --scope --quiet "$runuser_bin" --preserve-environment -u "$(id -un)" -- \
      /usr/bin/env "${scope_environment[@]}" /usr/bin/bash "$script_path" "$executable"
  elif grep -Eq 'system\.slice/.*\.service' /proc/self/cgroup; then
    echo 'Backup restore preflight test is in a systemd service cgroup, but no transient scope is available.' >&2
    exit 1
  fi
fi

temporary="$(mktemp -d "${TMPDIR:-/tmp}/v2rayn-backup-restore-preflight.XXXXXX")"
app_pid=""
cleanup() {
  if [[ -n "$app_pid" ]] && kill -0 "$app_pid" 2>/dev/null; then
    kill -TERM "$app_pid" 2>/dev/null || true
    for _ in {1..40}; do
      kill -0 "$app_pid" 2>/dev/null || break
      sleep 0.1
    done
    kill -KILL "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
  rm -rf "$temporary"
}
trap cleanup EXIT HUP INT TERM

port() {
  python3 - <<'PY'
import socket
with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
}

lan_address() {
  python3 - <<'PY'
import socket
with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
    sock.connect(("192.0.2.1", 9))
    print(sock.getsockname()[0])
PY
}

config_fingerprint() {
  python3 - "$1" <<'PY'
import hashlib
import sys
from pathlib import Path
root = Path(sys.argv[1])
for path in sorted(p for p in root.rglob("*") if p.is_file()):
    print(hashlib.sha256(path.read_bytes()).hexdigest(), path.relative_to(root).as_posix())
PY
}

run_refusal_case() {
  local name="$1" listener="$2" scheme="$3" expected_key="$4" missing_setsid="$5"
  local case_root="$temporary/$name" data_home="$temporary/$name-data" key url pid lock_path token
  local curl_tls=() process_env=()
  mkdir -p "$case_root" "$data_home"
  key="restore-preflight-$name-$(python3 -c 'import secrets; print(secrets.token_hex(12))')"
  local listen_port
  listen_port="$(port)"
  if [[ "$scheme" == https ]]; then
    url="https://127.0.0.1:$listen_port"
    curl_tls=(--insecure)
    openssl req -x509 -newkey rsa:2048 -nodes -days 1 \
      -subj /CN=localhost \
      -keyout "$case_root/key.pem" -out "$case_root/cert.pem" >/dev/null 2>&1
    openssl pkcs12 -export -out "$case_root/cert.pfx" \
      -inkey "$case_root/key.pem" -in "$case_root/cert.pem" \
      -passout pass:restore-smoke >/dev/null 2>&1
    process_env+=(
      "Kestrel__Endpoints__Https__Url=https://127.0.0.1:$listen_port"
      "Kestrel__Endpoints__Https__Certificate__Path=$case_root/cert.pfx"
      'Kestrel__Endpoints__Https__Certificate__Password=restore-smoke'
    )
  else
    url="http://$listener:$listen_port"
    process_env+=("ASPNETCORE_URLS=$url")
  fi
  if [[ "$missing_setsid" == true ]]; then
    mkdir -p "$case_root/empty-path"
    process_env+=("PATH=$case_root/empty-path")
  fi

  env \
    -u ASPNETCORE_URLS -u ASPNETCORE_HTTP_PORTS -u ASPNETCORE_HTTPS_PORTS \
    -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
    -u DOTNET_RUNNING_IN_CONTAINER -u container \
    "V2RAYN_WEB_API_KEY=$key" \
    V2RAYN_WEB_AUTOSTART=false \
    V2RAYN_WEB_INTERACTIVE_LAUNCH=1 \
    "V2RAYN_DATA_HOME=$data_home" \
    "DOTNET_BUNDLE_EXTRACT_BASE_DIR=$case_root/dotnet-bundle" \
    "${process_env[@]}" \
    "$executable" --foreground --no-open >"$case_root/runtime.log" 2>&1 &
  app_pid=$!

  local ready=false
  for _ in {1..100}; do
    if ! kill -0 "$app_pid" 2>/dev/null; then
      cat "$case_root/runtime.log" >&2
      echo "$name WebAPI exited before health became available." >&2
      return 1
    fi
    if curl --noproxy '*' "${curl_tls[@]}" --silent --show-error --fail \
      -D "$case_root/health.headers" \
      "$url/api/health" -o "$case_root/health.json" 2>/dev/null; then
      ready=true
      break
    fi
    sleep 0.2
  done
  [[ "$ready" == true ]] || { cat "$case_root/runtime.log" >&2; echo "$name WebAPI failed to become healthy." >&2; return 1; }

  curl --noproxy '*' "${curl_tls[@]}" --silent --show-error --fail \
    -H 'Content-Type: application/json' \
    --data "{\"key\":\"$key\"}" \
    "$url/api/auth/login" >"$case_root/login.json"
  token="$(python3 - "$case_root/login.json" <<'PY'
import json, sys
response = json.load(open(sys.argv[1], encoding="utf-8"))
assert response["success"] is True, response
print(response["data"]["token"])
PY
)"

  curl --noproxy '*' "${curl_tls[@]}" --silent --show-error --fail \
    -H "Authorization: Bearer $token" \
    "$url/api/backup/download" -o "$case_root/backup.zip"
  python3 - "$case_root/backup.zip" <<'PY'
from pathlib import Path
import sys
import zipfile
path = Path(sys.argv[1])
with zipfile.ZipFile(path, "a", compression=zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("guiConfigs/restore-preflight-marker.txt", "must-not-be-installed")
PY

  local config_directory="$data_home/v2rayN/guiConfigs"
  test -s "$config_directory/guiNConfig.json"
  local before_config="$case_root/config.before" after_config="$case_root/config.after"
  local lock_path="$data_home/v2rayN/v2rayN.WebAPI.instance.lock"
  local owner_pid
  owner_pid="$(tr -d '\r\n' <"$lock_path")"
  [[ "$owner_pid" == "$app_pid" ]] || {
    echo "$name instance lock PID does not match the launched process." >&2
    return 1
  }
  curl --noproxy '*' "${curl_tls[@]}" --silent --show-error \
    -H "Authorization: Bearer $token" \
    "$url/api/status" >"$case_root/status-before.json"
  python3 - "$case_root/status-before.json" <<'PY'
import json, sys
status = json.load(open(sys.argv[1], encoding="utf-8"))["data"]
assert status["runtimeState"] == "stopped", status
assert status["coreRunning"] is False, status
assert not status["coreProcessIds"], status
PY
  config_fingerprint "$config_directory" >"$before_config"
  python3 - "$case_root/health.json" <<'PY'
import json, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
assert data == {"status": "ok"}, data
PY
  test ! -e "$config_directory/restore-preflight-marker.txt"

  local status response_key
  status="$(curl --noproxy '*' "${curl_tls[@]}" --silent --show-error \
    -o "$case_root/restore.json" -w '%{http_code}' \
    -H "Authorization: Bearer $token" \
    -F "file=@$case_root/backup.zip;type=application/zip" \
    "$url/api/backup/restore")"
  response_key="$(python3 - "$case_root/restore.json" <<'PY'
import json, sys
response = json.load(open(sys.argv[1], encoding="utf-8"))
assert response["success"] is False, response
print(response["messageKey"])
PY
)"
  [[ "$response_key" == "$expected_key" ]] || {
    cat "$case_root/restore.json" >&2
    echo "$name restore refusal returned $response_key; expected $expected_key." >&2
    return 1
  }

  kill -0 "$app_pid"
  config_fingerprint "$config_directory" >"$after_config"
  cmp "$before_config" "$after_config"
  test ! -e "$config_directory/restore-preflight-marker.txt"
  test ! -e "$data_home/v2rayN/v2rayn-WebAPI-restore-state.json"
  test "$(tr -d '\r\n' <"$lock_path")" = "$app_pid"
  curl --noproxy '*' "${curl_tls[@]}" --silent --show-error --fail \
    -D "$case_root/health-after.headers" -o "$case_root/health-after.json" \
    "$url/api/health"
  python3 - "$case_root/health-after.json" "$case_root/status-before.json" <<'PY'
import json, sys
assert json.load(open(sys.argv[1], encoding="utf-8")) == {"status": "ok"}
status = json.load(open(sys.argv[2], encoding="utf-8"))["data"]
assert status["runtimeState"] == "stopped" and not status["coreProcessIds"], status
PY
  [[ "$(tr -d '\r\n' <"$lock_path")" == "$app_pid" ]] || { echo "$name refusal changed the healthy instance PID." >&2; return 1; }
  printf '%s listener refusal passed (messageKey=%s; config, Core state, owner PID and health unchanged).\n' "$name" "$response_key"

  kill -TERM "$app_pid"
  for _ in {1..100}; do
    kill -0 "$app_pid" 2>/dev/null || break
    sleep 0.1
  done
  if kill -0 "$app_pid" 2>/dev/null; then
    cat "$case_root/runtime.log" >&2
    echo "$name WebAPI did not stop during cleanup." >&2
    return 1
  fi
  wait "$app_pid" || true
  app_pid=""
}

https_port="$(port)"
openssl req -x509 -newkey rsa:2048 -nodes -days 1 \
  -subj /CN=localhost \
  -keyout "$temporary/key.pem" -out "$temporary/cert.pem" >/dev/null 2>&1
openssl pkcs12 -export -out "$temporary/cert.pfx" \
  -inkey "$temporary/key.pem" -in "$temporary/cert.pem" \
  -passout pass:restore-smoke >/dev/null 2>&1

run_refusal_case \
  https-only "https://127.0.0.1:$https_port" https \
  backup.restoreProbeUnavailable false
run_refusal_case \
  explicit-lan "$(lan_address)" http \
  backup.restoreProbeUnavailable false
run_refusal_case \
  missing-setsid 127.0.0.1 http \
  backup.restoreSetSidUnavailable true

printf 'Linux backup-restore preflight refusal tests passed.\n'
