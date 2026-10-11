# Ported from ssh-tunnel-for-ios/MobileCore/keyprovision/helper/install-v1.sh.
# Fixed Linux/GNU helper. Invoked under an external 60-second timeout.
# No caller-controlled command text, eval, source, or path is accepted.
set -fu
umask 077
LC_ALL=C
export LC_ALL
MAX_FILE=1048576
operation=''
locked=0
readonly_session=0
written=0
snapshot=''
for tool in stat sha256sum dd od grep sed id getent mkdir rm rmdir cat tail; do
    command -v "$tool" >/dev/null 2>&1 || exit 70
done
stat --version >/dev/null 2>&1 || exit 70
dd --version >/dev/null 2>&1 || exit 70
uid=$(id -u) || exit 70
home=$(getent passwd "$uid" | sed -n 's/^[^:]*:[^:]*:[^:]*:[^:]*:[^:]*:\([^:]*\):[^:]*$/\1/p')
case "$home" in /*) ;; *) exit 70 ;; esac
[ "$home" = "${HOME-}" ] || exit 70
[ ! -L "$home" ] && [ -d "$home" ] || exit 70
physical=$(cd "$home" && pwd -P) || exit 70
[ "$physical" = "$home" ] || exit 70
sshdir=$home/.ssh
file=$sshdir/authorized_keys
lock=$home/.ssh-tunnel-install.lock

safe_owner_mode() {
    owner=$(stat -c %u -- "$1") || return 1
    mode=$(stat -c %a -- "$1") || return 1
    [ "$owner" = "$uid" ] || return 1
    case "$mode" in ''|*[!0-7]*) return 1 ;; esac
    [ $((0$mode & 022)) -eq 0 ]
}
safe_owner_mode "$home" || exit 70
cleanup() {
    if [ "$locked" -eq 1 ] && [ ! -L "$lock" ] && [ -d "$lock" ]; then
        current=$(stat -c '%d:%i:%u' -- "$lock" 2>/dev/null) || return
        if [ "$current" = "$lock_identity" ] && [ ! -L "$lock/owner" ] && [ -f "$lock/owner" ]; then
            owner_token=$(cat -- "$lock/owner" 2>/dev/null) || return
            if [ "$owner_token" = "$operation" ]; then
                rm -- "$lock/owner" 2>/dev/null || return
                rmdir -- "$lock" 2>/dev/null || :
            fi
        fi
    fi
}
trap cleanup EXIT
trap 'exit 71' HUP INT TERM

# Emits a single byte without storing binary/NUL values in shell variables.
emit_byte() { octal=$(printf '%03o' "$1"); printf '%b' "\\$octal"; }
frame_header() {
    printf SKP1
    emit_byte "$1"
    printf '\000\000\000'
    emit_byte $(( $2 / 16777216 % 256 ))
    emit_byte $(( $2 / 65536 % 256 ))
    emit_byte $(( $2 / 256 % 256 ))
    emit_byte $(( $2 % 256 ))
}
reply() {
    if [ -n "$2" ]; then
        json=$(printf '{"version":1,"operationId":"%s","action":"%s","status":"%s","identity":%s}' "$operation" "$action" "$1" "$2")
    else
        json=$(printf '{"version":1,"operationId":"%s","action":"%s","status":"%s"}' "$operation" "$action" "$1")
    fi
    frame_header 2 "${#json}"
    printf '%s' "$json"
}
fail_reply() { reply "$1" ''; exit 72; }
inspect() {
    if [ -L "$sshdir" ] || { [ -e "$sshdir" ] && [ ! -d "$sshdir" ]; }; then return 1; fi
    if [ -d "$sshdir" ]; then safe_owner_mode "$sshdir" || return 1; fi
    if [ -L "$file" ]; then return 1; fi
    if [ ! -e "$file" ]; then
        size=0
        identity='{"exists":false,"device":0,"inode":0,"size":0,"sha256":""}'
        return 0
    fi
    [ -f "$file" ] && safe_owner_mode "$file" || return 1
    links=$(stat -c %h -- "$file") || return 1
    [ "$links" -eq 1 ] || return 1
    size=$(stat -c %s -- "$file") || return 1
    [ "$size" -le "$MAX_FILE" ] || return 1
    device=$(stat -c %d -- "$file") || return 1
    inode=$(stat -c %i -- "$file") || return 1
    digest=$(sha256sum < "$file") || return 1
    digest=${digest%% *}
    identity=$(printf '{"exists":true,"device":%s,"inode":%s,"size":%s,"sha256":"%s"}' "$device" "$inode" "$size" "$digest")
}
file_reply() {
    reply "$1" "$identity"
    frame_header 3 "$size"
    if [ "$size" -gt 0 ]; then
        dd if="$file" bs="$size" count=1 iflag=fullblock status=none || exit 73
    fi
}
acquire() {
    [ "$locked" -eq 0 ] || return 0
    mkdir -m 700 -- "$lock" 2>/dev/null || return 1
    # On uncertain initialization, leave the lock for manual inspection.
    safe_owner_mode "$lock" || return 1
    lock_identity=$(stat -c '%d:%i:%u' -- "$lock") || return 1
    printf '%s' "$operation" > "$lock/owner" || return 1
    locked=1
}

while :; do
    header=$(dd bs=12 count=1 iflag=fullblock status=none | od -An -v -tu1) || exit 74
    [ -n "$header" ] || exit 0
    # od produces numeric tokens only; splitting is intentional, globbing is off.
    # shellcheck disable=SC2086
    set -- $header
    [ "$#" -eq 12 ] || exit 74
    [ "$1 $2 $3 $4 $5 $6 $7 $8" = '83 75 80 49 1 0 0 0' ] || exit 74
    length=$(( $9 * 16777216 + ${10} * 65536 + ${11} * 256 + ${12} ))
    [ "$length" -gt 0 ] && [ "$length" -le 16384 ] || exit 74
    request=$(dd bs="$length" count=1 iflag=fullblock status=none) || exit 74
    [ "${#request}" -eq "$length" ] || exit 74
    incoming=$(printf '%s' "$request" | sed -n 's/^{"version":1,"operationId":"\([0-9a-fA-F-]*\)","action":.*$/\1/p')
    printf '%s' "$incoming" | grep -Eq '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' || exit 74
    if [ -z "$operation" ]; then operation=$incoming; fi
    [ "$operation" = "$incoming" ] || exit 74
    action=$(printf '%s' "$request" | sed -n 's/^{"version":1,"operationId":"[0-9a-fA-F-]*","action":"\([a-z]*\)".*$/\1/p')
    case "$action" in
    probe|read|reconcile)
        expected_request=$(printf '{"version":1,"operationId":"%s","action":"%s"}' "$operation" "$action")
        [ "$request" = "$expected_request" ] || exit 74
        [ "$written" -eq 0 ] || exit 74
        if [ "$action" = reconcile ]; then readonly_session=1; fi
        inspect || fail_reply denied
        if [ "$action" = probe ]; then reply ok "$identity"; continue; fi
        acquire || fail_reply busy
        inspect || fail_reply denied
        snapshot=$identity
        file_reply ok
        ;;
    append)
        [ "$readonly_session" -eq 0 ] && [ "$written" -eq 0 ] && [ "$locked" -eq 1 ] && [ -n "$snapshot" ] || exit 74
        expected=$(printf '%s' "$request" | sed -n 's/^.*,"expected":\({[^{}]*}\),"publicKey":"[^"]*"}$/\1/p')
        encoded_key=$(printf '%s' "$request" | sed -n 's/^.*,"publicKey":"\([^"]*\)"}$/\1/p')
        key=$(printf '%s' "$encoded_key" | sed 's/\\n$//')
        printf '%s' "$key" | grep -Eq '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521)) [A-Za-z0-9+/]+={0,2}$' || exit 74
        [ "${#key}" -lt 8192 ] || exit 74
        expected_request=$(printf '{"version":1,"operationId":"%s","action":"append","expected":%s,"publicKey":"%s\\n"}' "$operation" "$expected" "$key")
        [ "$request" = "$expected_request" ] || exit 74
        [ "$expected" = "$snapshot" ] || fail_reply conflict
        inspect || fail_reply denied
        [ "$identity" = "$snapshot" ] || fail_reply conflict
        extra=0
        if [ "$size" -gt 0 ]; then
            last=$(tail -c 1 -- "$file" | od -An -tu1) || fail_reply conflict
            [ "$last" -eq 10 ] || extra=1
        fi
        [ $((size + extra + ${#key} + 1)) -le "$MAX_FILE" ] || fail_reply denied
        written=1
        was_missing=0
        case "$snapshot" in '{"exists":false,'*) was_missing=1 ;; esac
        created_file=0
        if [ ! -d "$sshdir" ]; then mkdir -m 700 -- "$sshdir" || fail_reply writeUnknown; fi
        if [ ! -e "$file" ]; then
            (set -C; : > "$file") || fail_reply writeUnknown
            created_file=1
        fi
        # Recheck paths after creation; existing permissions are never rewritten.
        inspect || fail_reply writeUnknown
        if [ "$was_missing" -eq 1 ]; then
            [ "$created_file" -eq 1 ] && [ "$size" -eq 0 ] || fail_reply writeUnknown
        else
            [ "$created_file" -eq 0 ] && [ "$identity" = "$snapshot" ] || fail_reply writeUnknown
        fi
        if [ "$extra" -eq 1 ]; then printf '\n' >> "$file" || fail_reply writeUnknown; fi
        printf '%s\n' "$key" >> "$file" || fail_reply writeUnknown
        inspect || fail_reply writeUnknown
        file_reply added
        exit 0
        ;;
    *) exit 74 ;;
    esac
done
