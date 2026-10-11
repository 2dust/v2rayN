# SSH nodes

[简体中文：密码登录与一键配置免密](ssh-key-setup.zh-Hans.md)

SSH is a native profile type in both the Avalonia desktop client (macOS/Linux)
and the Windows WPF client. Select **Add SSH server**, enter the SSH endpoint and
username, then choose password or private-key-file authentication. An encrypted
private key may also require its passphrase.

New nodes default to **password authentication**. Saving a password node lets the
core authenticate automatically; setting up public-key login is optional.

## One-click key login

Fill in the server address, port and login username, then select **Configure key
login**. Confirm the server fingerprint through a trusted channel before any
credentials are sent. The setup window fixes this endpoint and account for the
duration of the operation.

The window offers **Reuse a local private key** and **Generate a new private key**:

- Discovery checks the current form, previously configured SSH nodes, the local
  managed-key directory, and ordinary files directly in `~/.ssh`. It does not
  search subdirectories. Browse can select a key elsewhere.
- Each candidate shows its path, algorithm, SHA256 public-key fingerprint and
  encryption state where available. Encrypted keys need their passphrase before
  they can be fully inspected. Matching public identities are labelled. Public
  keys, `config`, and `known_hosts` are not private-key candidates.
- Reuse references the existing file without copying, overwriting or changing its
  permissions. It first tries **key-only authentication**. Only authentication
  refusal prompts for the account password to install the public key; network
  errors and host-key mismatches never trigger password fallback.
- Generation first authenticates with the supplied account password, then creates
  a new Ed25519 OpenSSH key. A private-key passphrase is optional and distinct from
  the server password. Each explicit generation creates a new file; existing keys
  and the nodes referencing them are preserved.

Installation appends the public key to **that login account's** standard
`~/.ssh/authorized_keys`, then closes the password connection and verifies login
with a new key-only connection. Already present keys are not appended again,
including keys carrying `from=`, `command=` or other restrictions. Existing bytes
and permissions are preserved; newly created directories/files use 0700/0600.

Successful verification fills the editor's private-key path and authentication
mode. **Save the node** to persist it. Setup does not activate the node or change
the running proxy. The saved selection becomes the first shared-key candidate
for later nodes; each setup still offers reuse or generation. Public-key login
success is separate from forwarding permission: use the normal node connectivity
test to check HTTP/HTTPS proxying.

### Storage and recovery

Generated keys and non-secret setup journals are stored under `sshKeys`, next to
`guiConfigs` in the active application data directory. The current configuration
backup/WebDAV workflow copies `guiConfigs` only, so **managed private keys are not
included in configuration backups or synchronization**. Moving to another machine
requires separately preserving a key or generating and installing a new one.
Unix private keys require 0600 permissions (and the managed directory 0700);
Windows managed keys use an ACL limited to the current user. Broad external-key
permissions are reported, never silently repaired.

The one-time installation password is cleared after each attempt and is never
stored in the node or installation journal. An encrypted key's passphrase follows
the existing node credential storage behavior when saved; there is no separate
global passphrase cache. Key files are retained when a node is deleted or an
installation fails. Canceling editing does not change the default key selection.

Automatic installation supports Linux/OpenSSH with a command-capable account,
POSIX shell and the GNU tools used by the bundled helper. It does not edit
`sshd_config`, disable passwords, use sudo, or restart SSH. Symbolic links, unsafe
ownership/permissions, concurrent file changes, malformed authorization files,
nonstandard authorized-key locations, restricted shells and MFA-only password
flows require manual handling. The setup window exposes the public key and
manual instructions for these cases.

Cancellation or a lost connection **does not undo an already installed public
key**. The local key and a non-secret operation journal are retained. Retry uses
the same key, checks authentication first and reads/deduplicates authorization
before any append. A write/verification failure never silently switches the node
to private-key authentication. A missing or changed preferred private key is not
automatically replaced.

The editor retrieves the server public key on the first save. Verify the displayed
SHA256 fingerprint through a trusted channel before accepting. Fetching a key
does not authenticate the server by itself. The probe stops during key exchange,
before sending any login credentials. A pasted OpenSSH-format public key can be
accepted with **Confirm pasted public key** instead.

The accepted key is pinned to the address and port. Changing either requires a
new confirmation; changing the login username does not. Key mismatches fail
closed. Use **Fetch / refresh public key** and explicitly confirm a legitimate
server key rotation. No existing pin is silently replaced.

Local ports, routing, statistics and system proxy integration use v2rayN's normal
settings. SSH always selects sing-box, including when the default core is Xray.
For a policy group or proxy chain containing SSH, select sing-box for the group;
Xray groups report an unsupported-protocol error.

SSH forwards TCP only. Enabling TUN does not add UDP-over-SSH support. UDP traffic
routed to SSH fails rather than falling back to a direct connection. Simple
proxied UDP DNS servers are generated as TCP DNS servers at the same address and
port. Proxied custom UDP/QUIC/HTTP3 DNS must be changed to TCP/TLS/HTTPS.

Private-key contents are not copied into profiles. Only the local file path is
saved. Passwords/passphrases follow existing profile-storage and backup behavior;
they are excluded from SSH editor error messages. Paths must exist on the machine
running the core. SSH Agent, SSH share URIs, SSH certificates, custom UDP channels
and automatic migration of custom JSON profiles are not supported.

## Development and verification

Requires .NET 10 and the repository's GlobalHotKeys submodule:

Key-setup and full-config integration were verified with sing-box 1.14.3. The current repository
already emits newer DNS fields (such as `preferred_by`) which sing-box 1.13.11
does not accept; use the core version matching this source checkout.

```sh
git submodule update --init --recursive
dotnet build v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj
dotnet build v2rayN/v2rayN/v2rayN.csproj -p:EnableWindowsTargeting=true
dotnet run --project v2rayN/ServiceLib.Tests -- --treenode-filter '/*/*/Ssh*/*'
```

The loopback integration test is skipped unless both environment variables are
provided. It starts a disposable Python SSH/HTTP/HTTPS fixture, generates its own
keys, restricts forwarding to its HTTP ports, and stops all child processes when
finished. Install `paramiko` in a temporary virtual environment, then run:

```sh
SSH_TEST_PYTHON=/absolute/path/to/venv/bin/python \
SSH_TEST_SINGBOX=/absolute/path/to/sing-box \
dotnet run --project v2rayN/ServiceLib.Tests -- --treenode-filter '/*/*/Ssh*/*'
```

Real public-key installation tests use a disposable Linux/OpenSSH Docker fixture
with no host mounts and only an ephemeral loopback SSH port. They include password
authentication, generated/reused/encrypted keys, restricted-key deduplication,
permission and host-key failures, concurrent edits, cancellation/retry, and
sing-box HTTP/HTTPS forwarding:

```sh
docker build -t v2rayn-ssh-key-setup:test v2rayN/ServiceLib.Tests/Fixtures/openssh-key-setup
SSH_SETUP_DOCKER_IMAGE=v2rayn-ssh-key-setup:test \
SSH_TEST_SINGBOX=/absolute/path/to/sing-box \
dotnet run --project v2rayN/ServiceLib.Tests -- --treenode-filter '/*/*/SshKeySetup*/*'
```

Coverage includes endpoint-bound trust, explicit acceptance/cancellation,
Ed25519/ECDSA/RSA key exchange, no authentication during probing, cancellation of
a stalled handshake, password and encrypted/unencrypted key authentication,
HTTP/HTTPS forwarding, invalid credentials and changed host keys, profile
persistence, DNS policy, and sing-box configuration checks.

Run development builds in a writable, separate output directory with
`V2RAYN_LOCAL_APPLICATION_DATA_V2=0` to keep data separate from the installed
application. Cross-compilation alone does not verify Windows/Linux runtime UI.
