#!/usr/bin/env bash
set -euo pipefail

source_executable="${1:?Usage: test-startup-security.sh <native-v2rayN.WebAPI-executable>}"
source_executable="$(realpath "$source_executable")"
test -x "$source_executable"
command -v curl >/dev/null
command -v python3 >/dev/null

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

# Run from an isolated copy so a developer's executable-directory .env is not read or copied
# into the test; individual cases create controlled configuration files beside this apphost.
runtime_directory="$temporary/runtime"
source_directory="$(dirname "$source_executable")"
mkdir -p "$runtime_directory" "$temporary/other-working-directory"
for item in "$source_directory"/* "$source_directory"/.[!.]* "$source_directory"/..?*; do
  [[ -e "$item" || -L "$item" ]] || continue
  [[ "${item##*/}" == ".env" ]] && continue
  cp -a -- "$item" "$runtime_directory/"
done
executable="$runtime_directory/v2rayN.WebAPI"
test -x "$executable"

new_port() {
  python3 - <<'PY'
import socket

with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
}

assert_rejected_without_key() {
  local deployment="$1" data_home="$temporary/$1-data" output="$temporary/$1-rejected.log" status
  local markers=()
  case "$deployment" in
    systemd) markers=(INVOCATION_ID=startup-security-test) ;;
    container) markers=(DOTNET_RUNNING_IN_CONTAINER=true) ;;
    *) echo "Unknown deployment mode: $deployment" >&2; exit 2 ;;
  esac

  set +e
  if (
    cd "$temporary/other-working-directory"
    exec env -u V2RAYN_WEB_API_KEY \
      -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
      -u DOTNET_RUNNING_IN_CONTAINER -u container \
      "${markers[@]}" V2RAYN_DATA_HOME="$data_home" \
      "$executable" --foreground --no-open
  ) >"$output" 2>&1; then
    status=0
  else
    status=$?
  fi
  set -e

  [[ "$status" -eq 1 ]] || { cat "$output" >&2; echo "$deployment did not fail closed (exit $status)." >&2; exit 1; }
  grep -Fqx 'Management Key is required in supervised/container deployments. Set V2RAYN_WEB_API_KEY before starting v2rayN.WebAPI.' "$output"
  test ! -e "$data_home"
}

wait_for_health() {
  local base_url="$1" output="$2" healthy=false
  for _ in {1..60}; do
    if ! kill -0 "$web_pid" 2>/dev/null; then
      cat "$output" >&2
      echo "Web process exited before becoming healthy." >&2
      exit 1
    fi
    if curl --silent --show-error --fail "$base_url/api/health" -o "$temporary/health.json" 2>/dev/null \
        && python3 - "$temporary/health.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    assert json.load(handle) == {"status": "ok"}
PY
    then
      healthy=true
      break
    fi
    sleep 0.5
  done
  if [[ "$healthy" != true ]]; then
    cat "$output" >&2
    echo "Web process did not become healthy." >&2
    exit 1
  fi
}

stop_instance() {
  local output="$1" stopped=false state exit_code
  kill -TERM "$web_pid"
  for _ in {1..80}; do
    state="$(ps -o stat= -p "$web_pid" 2>/dev/null | tr -d ' ' || true)"
    if [[ -z "$state" || "$state" == *Z* ]]; then
      stopped=true
      break
    fi
    sleep 0.25
  done
  if [[ "$stopped" != true ]]; then
    cat "$output" >&2
    echo "Web process did not stop gracefully." >&2
    exit 1
  fi
  set +e
  wait "$web_pid"
  exit_code=$?
  set -e
  web_pid=""
  if [[ "$exit_code" -ne 0 ]]; then
    cat "$output" >&2
    echo "Web process exited with status $exit_code." >&2
    exit 1
  fi
}

assert_rejected_without_key systemd
assert_rejected_without_key container

assert_remote_listener_rejected() {
  local source="$1" output="$temporary/listener-$1.log" status
  local settings=() arguments=()
  case "$source" in
    urls) settings=(ASPNETCORE_URLS=http://0.0.0.0:5080) ;;
    ipv6) settings=('ASPNETCORE_URLS=http://[::]:5080') ;;
    lan) settings=(ASPNETCORE_URLS=http://192.168.1.20:5080) ;;
    multiple) settings=('ASPNETCORE_URLS=http://localhost:5080;http://0.0.0.0:5081') ;;
    cli) settings=(ASPNETCORE_URLS=http://localhost:5080); arguments=(--urls http://0.0.0.0:5081) ;;
    cli-equals) arguments=(--urls=http://0.0.0.0:5081) ;;
    http-ports) settings=(ASPNETCORE_HTTP_PORTS=5080) ;;
    https-ports) settings=(ASPNETCORE_HTTPS_PORTS=5443) ;;
    kestrel) settings=(ASPNETCORE_URLS=http://localhost:5080 Kestrel__Endpoints__Http__Url=http://0.0.0.0:5081) ;;
  esac
  if timeout 15 env -u V2RAYN_WEB_API_KEY \
      -u ASPNETCORE_URLS -u ASPNETCORE_HTTP_PORTS -u ASPNETCORE_HTTPS_PORTS \
      -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
      -u DOTNET_RUNNING_IN_CONTAINER -u container \
      "${settings[@]}" V2RAYN_DATA_HOME="$temporary/listener-$source-data" \
      "$executable" --foreground --no-open "${arguments[@]}" >"$output" 2>&1; then
    status=0
  else
    status=$?
  fi
  [[ "$status" -eq 1 ]] || { cat "$output" >&2; echo "$source listener did not fail closed (exit $status)." >&2; exit 1; }
  grep -Fq 'Management Key is required for non-loopback Web listeners.' "$output"
}

for source in urls ipv6 lan multiple cli cli-equals http-ports https-ports kestrel; do
  assert_remote_listener_rejected "$source"
done

assert_malformed_configuration_fails() {
  local output="$temporary/malformed-env.log" status
  printf 'V2RAYN_WEB_API_KEY=must-not-be-echoed"\n' > "$runtime_directory/.env"
  if (
    cd "$temporary/other-working-directory"
    exec env -u V2RAYN_WEB_API_KEY -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
      -u DOTNET_RUNNING_IN_CONTAINER -u container "$executable" --foreground --no-open
  ) >"$output" 2>&1; then
    status=0
  else
    status=$?
  fi
  rm -f "$runtime_directory/.env"
  [[ "$status" -eq 1 ]] || { cat "$output" >&2; echo "Malformed .env did not fail startup (exit $status)." >&2; exit 1; }
  grep -Fq 'Invalid v2rayN.WebAPI environment configuration:' "$output"
  ! grep -Fq 'must-not-be-echoed' "$output"
}

assert_malformed_configuration_fails

assert_invalid_management_key_fails() {
  local source="$1" length="$2" key output data_home status
  output="$temporary/invalid-key-$source-$length.log"
  data_home="$temporary/invalid-key-$source-$length-data"
  key="$(python3 -c 'import sys; print("x" * int(sys.argv[1]))' "$length")"
  rm -f "$runtime_directory/.env"
  if [[ "$source" == dotenv ]]; then
    printf 'V2RAYN_WEB_API_KEY=%s\n' "$key" > "$runtime_directory/.env"
    chmod 600 "$runtime_directory/.env"
  fi

  set +e
  if (
    cd "$temporary/other-working-directory"
    if [[ "$source" == environment ]]; then
      exec env -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
        -u DOTNET_RUNNING_IN_CONTAINER -u container \
        "V2RAYN_WEB_API_KEY=$key" V2RAYN_DATA_HOME="$data_home" \
        "$executable" --foreground --no-open
    else
      exec env -u V2RAYN_WEB_API_KEY \
        -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
        -u DOTNET_RUNNING_IN_CONTAINER -u container \
        V2RAYN_DATA_HOME="$data_home" "$executable" --foreground --no-open
    fi
  ) >"$output" 2>&1; then
    status=0
  else
    status=$?
  fi
  set -e
  rm -f "$runtime_directory/.env"

  [[ "$status" -eq 1 ]] || { cat "$output" >&2; echo "$source key with length $length did not fail startup (exit $status)." >&2; exit 1; }
  grep -Fqx 'V2RAYN_WEB_API_KEY length must be between 12 and 4096 characters.' "$output"
  ! grep -Fq "$key" "$output"
  test ! -e "$data_home"
}

start_with_valid_management_key_boundary() {
  local source="$1" length="$2" key port data_home output url
  local key_settings=()
  key="$(python3 -c 'import sys; print("b" * int(sys.argv[1]))' "$length")"
  port="$(new_port)"
  data_home="$temporary/valid-key-$source-$length-data"
  output="$temporary/valid-key-$source-$length.log"
  url="http://127.0.0.1:$port"
  rm -f "$runtime_directory/.env"

  if [[ "$source" == dotenv ]]; then
    printf 'V2RAYN_WEB_API_KEY=%s\n' "$key" > "$runtime_directory/.env"
    chmod 600 "$runtime_directory/.env"
  else
    key_settings=("V2RAYN_WEB_API_KEY=$key")
  fi

  (
    cd "$temporary/other-working-directory"
    exec env -u V2RAYN_WEB_API_KEY \
      -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
      -u DOTNET_RUNNING_IN_CONTAINER -u container \
      -u ASPNETCORE_URLS -u ASPNETCORE_HTTP_PORTS -u ASPNETCORE_HTTPS_PORTS \
      "${key_settings[@]}" \
      V2RAYN_DATA_HOME="$data_home" V2RAYN_WEB_AUTOSTART=false ASPNETCORE_URLS="$url" \
      "$executable" --foreground --no-open
  ) >"$output" 2>&1 &
  web_pid=$!
  wait_for_health "$url" "$output"
  stop_instance "$output"
  rm -f "$runtime_directory/.env"
}

assert_invalid_management_key_fails environment 11
assert_invalid_management_key_fails environment 4097
assert_invalid_management_key_fails dotenv 11
assert_invalid_management_key_fails dotenv 4097
start_with_valid_management_key_boundary environment 12
start_with_valid_management_key_boundary dotenv 4096

# Native interactive first run remains available without SSH or a preconfigured key.
native_port="$(new_port)"
native_data="$temporary/native-data"
native_log="$temporary/native-first-run.log"
native_url="http://127.0.0.1:$native_port"
native_key="first-run-$(python3 -c 'import secrets; print(secrets.token_hex(24))')"
(
  cd "$temporary/other-working-directory"
  exec env -u V2RAYN_WEB_API_KEY \
    -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
    -u DOTNET_RUNNING_IN_CONTAINER -u container \
    V2RAYN_DATA_HOME="$native_data" \
    V2RAYN_WEB_AUTOSTART=true \
    ASPNETCORE_URLS="$native_url" \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR="$temporary/native-bundle" \
    "$executable" --foreground --no-open
) >"$native_log" 2>&1 &
web_pid=$!
wait_for_health "$native_url" "$native_log"

curl --silent --show-error --fail "$native_url/api/setup/status" >"$temporary/setup-status.json"
python3 - "$temporary/setup-status.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    status = json.load(handle)
assert status["setupRequired"] is True, status
assert status["setupAllowedFromThisRequest"] is True, status
PY

setup_body="$(python3 -c 'import json,sys; print(json.dumps({"key":sys.argv[1],"confirmKey":sys.argv[1]}))' "$native_key")"
curl --silent --show-error --fail \
  -H 'Content-Type: application/json' \
  --data "$setup_body" \
  "$native_url/api/setup" >"$temporary/setup.json"
session_token="$(python3 - "$temporary/setup.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
assert response["setupRequired"] is False, response
print(response["token"])
PY
)"
curl --silent --show-error --fail \
  -H "Authorization: Bearer $session_token" \
  "$native_url/api/status" >"$temporary/native-status.json"
python3 - "$temporary/native-status.json" <<'PY'
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
grep -Fq 'Core autostart was suppressed until first-run Management Key setup is complete.' "$native_log"
stop_instance "$native_log"

start_managed_with_key() {
  local deployment="$1" port="$2" data_home="$temporary/$1-data" output="$temporary/$1-key.log" url="http://127.0.0.1:$2"
  local key="managed-key-$(python3 -c 'import secrets; print(secrets.token_hex(24))')"
  local markers=()
  case "$deployment" in
    systemd) markers=(INVOCATION_ID=startup-security-test) ;;
    container) markers=(DOTNET_RUNNING_IN_CONTAINER=true) ;;
  esac

  env -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
    -u DOTNET_RUNNING_IN_CONTAINER -u container \
    "${markers[@]}" V2RAYN_WEB_API_KEY="$key" \
    V2RAYN_DATA_HOME="$data_home" V2RAYN_WEB_AUTOSTART=false \
    ASPNETCORE_URLS="$url" DOTNET_BUNDLE_EXTRACT_BASE_DIR="$temporary/$deployment-bundle" \
    "$executable" --foreground --no-open >"$output" 2>&1 &
  web_pid=$!
  wait_for_health "$url" "$output"
  stop_instance "$output"
}

start_managed_with_dotenv_key() {
  local deployment="$1" port="$2" data_home="$temporary/$1-dotenv-data" output="$temporary/$1-dotenv-key.log" url="http://127.0.0.1:$2"
  local key="dotenv-managed-key-$(python3 -c 'import secrets; print(secrets.token_hex(24))')"
  local markers=()
  case "$deployment" in
    systemd) markers=(INVOCATION_ID=startup-security-dotenv-test) ;;
    container) markers=(DOTNET_RUNNING_IN_CONTAINER=true) ;;
  esac

  printf 'V2RAYN_WEB_API_KEY=%s\nASPNETCORE_URLS=%s\nV2RAYN_WEB_AUTOSTART=false\n' \
    "$key" "$url" > "$runtime_directory/.env"
  chmod 600 "$runtime_directory/.env"
  (
    cd "$temporary/other-working-directory"
    exec env -u V2RAYN_WEB_API_KEY \
      -u V2RAYN_WEB_AUTOSTART -u ASPNETCORE_URLS -u ASPNETCORE_HTTP_PORTS \
      -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
      -u DOTNET_RUNNING_IN_CONTAINER -u container \
      "${markers[@]}" V2RAYN_DATA_HOME="$data_home" \
      DOTNET_BUNDLE_EXTRACT_BASE_DIR="$temporary/$deployment-dotenv-bundle" \
      "$executable" --foreground --no-open
  ) >"$output" 2>&1 &
  web_pid=$!
  wait_for_health "$url" "$output"

  python3 -c 'import json,sys; print(json.dumps({"key":sys.argv[1]}))' "$key" >"$temporary/$deployment-dotenv-login-request.json"
  curl --silent --show-error --fail -H 'Content-Type: application/json' \
    --data-binary "@$temporary/$deployment-dotenv-login-request.json" \
    "$url/api/auth/login" >"$temporary/$deployment-dotenv-login.json"
  local session_token
  session_token="$(python3 - "$temporary/$deployment-dotenv-login.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
assert response["success"] is True, response
print(response["data"]["token"])
PY
)"
  curl --silent --show-error --fail -H "Authorization: Bearer $session_token" \
    "$url/api/status" >"$temporary/$deployment-dotenv-status.json"
  python3 - "$temporary/$deployment-dotenv-status.json" <<'PY'
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
  stop_instance "$output"
  rm -f "$runtime_directory/.env"
}

start_managed_with_process_environment_precedence() {
  local port="$1" dotenv_port="$(new_port)" data_home="$temporary/systemd-precedence-data"
  local output="$temporary/systemd-precedence.log" url="http://127.0.0.1:$1"
  local dotenv_key="dotenv-shadowed-key-$(python3 -c 'import secrets; print(secrets.token_hex(24))')"
  local process_key="process-priority-key-$(python3 -c 'import secrets; print(secrets.token_hex(24))')"

  printf 'V2RAYN_WEB_API_KEY=%s\nASPNETCORE_URLS=http://127.0.0.1:%s\nV2RAYN_WEB_AUTOSTART=true\n' \
    "$dotenv_key" "$dotenv_port" > "$runtime_directory/.env"
  (
    cd "$temporary/other-working-directory"
    exec env -u INVOCATION_ID -u JOURNAL_STREAM -u NOTIFY_SOCKET \
      -u DOTNET_RUNNING_IN_CONTAINER -u container \
      INVOCATION_ID=startup-security-priority-test \
      V2RAYN_WEB_API_KEY="$process_key" \
      V2RAYN_WEB_AUTOSTART=false ASPNETCORE_URLS="$url" \
      V2RAYN_DATA_HOME="$data_home" \
      DOTNET_BUNDLE_EXTRACT_BASE_DIR="$temporary/systemd-priority-bundle" \
      "$executable" --foreground --no-open
  ) >"$output" 2>&1 &
  web_pid=$!
  wait_for_health "$url" "$output"

  python3 -c 'import json,sys; print(json.dumps({"key":sys.argv[1]}))' "$process_key" >"$temporary/systemd-priority-login-request.json"
  curl --silent --show-error --fail -H 'Content-Type: application/json' \
    --data-binary "@$temporary/systemd-priority-login-request.json" \
    "$url/api/auth/login" >"$temporary/systemd-priority-login.json"
  local session_token
  session_token="$(python3 - "$temporary/systemd-priority-login.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
assert response["success"] is True, response
print(response["data"]["token"])
PY
)"
  curl --silent --show-error --fail -H "Authorization: Bearer $session_token" \
    "$url/api/status" >"$temporary/systemd-priority-status.json"
  python3 - "$temporary/systemd-priority-status.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    response = json.load(handle)
status = response["data"]
assert response["success"] is True, response
assert status["coreRunning"] is False, status
assert status["runtimeState"] == "stopped", status
PY
  stop_instance "$output"
  rm -f "$runtime_directory/.env"
}

start_managed_with_dotenv_key systemd "$(new_port)"
start_managed_with_dotenv_key container "$(new_port)"
start_managed_with_process_environment_precedence "$(new_port)"
start_managed_with_key systemd "$(new_port)"
start_managed_with_key container "$(new_port)"

echo "Startup security tests passed (loopback first-run, remote listeners fail-closed across configuration sources, malformed .env rejection, systemd/container keys from .env or process environment)."
