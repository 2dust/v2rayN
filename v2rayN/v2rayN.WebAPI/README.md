# v2rayN.WebAPI

`v2rayN.WebAPI` is a headless ASP.NET Core host for v2rayN's existing `ServiceLib`. It exposes
REST APIs, authenticated SSE/runtime events, setup and sessions, settings and options,
profiles/subscriptions, routing and DNS, backups, updates, and the existing Core runtime bridge.
It can run without a browser, desktop session, or installed WebUI. The API has no dependency on
Vue, React, Vite, Node.js, npm, or any frontend manifest.

The API contract and the WebUI are separate deliverables. The reference implementation is
[Nozilla-X/v2rayN-WebUI](https://github.com/Nozilla-X/v2rayN-WebUI); users may install any
compatible static site instead.

## Start the API

For a native install, unpack the API release ZIP and start the executable from its install
directory:

```sh
cp .env.example .env
chmod 600 .env
# Set V2RAYN_WEB_API_KEY in .env for supervised, container, or remote deployments.
./v2rayN.WebAPI --foreground --no-open
```

The default listener is `http://127.0.0.1:5080`. A local native install may temporarily leave
`V2RAYN_WEB_API_KEY` empty when **all** listeners are loopback (`127.0.0.1`, `localhost`, or
`[::1]`), then complete first-run setup through a local browser and an installed WebUI.

For LAN / NAS / headless / container / systemd deployments, preconfigure
`V2RAYN_WEB_API_KEY`. Systemd and containers require it even on loopback. Any non-loopback
listener (including `0.0.0.0`, `[::]`, wildcard/hostname bindings, and LAN IPs) fails startup
without this environment Key, even if a Key was previously saved through local setup.
The check uses the effective ASP.NET Core configuration, including `ASPNETCORE_URLS`,
command-line `--urls`, `ASPNETCORE_HTTP_PORTS` / `ASPNETCORE_HTTPS_PORTS` (wildcard bindings),
and `Kestrel:Endpoints`, respecting their precedence. With multiple URLs, one non-loopback
listener is enough to require a Key; request Host headers never determine exposure.
For remote access, set a Key before selecting e.g. `ASPNETCORE_URLS=http://0.0.0.0:5080`.
Protect it with a firewall/VPN or an HTTPS reverse proxy; do not expose it directly to the Internet.

`GET /api/health` is a non-sensitive health probe. After authentication, `GET /api/status`
reports the running Backend version, commit, runtime identifier, and runtime capability fields.
The API option endpoints (including `/api/editor-options` and DNS editor options) are authoritative
for values derived from ServiceLib; clients should consume these instead of copying enums or
option lists.

## Authentication

- `V2RAYN_WEB_API_KEY` is the Management Key. Exchange it only at `POST /api/auth/login`.
- Use the returned session token as `Authorization: Bearer <session-token>` for protected REST
  endpoints. The Management Key is not a REST or SSE bearer token.
- `POST /api/auth/sse-ticket` returns a short-lived, one-time ticket for `/api/events`.
- Setup, login rate limits, session expiry/revocation, LAN/setup policy, DNS rebinding checks,
  and API authorization remain Backend-owned and apply equally to every WebUI.
- Static WebUI files are public. They do not grant, bypass, or alter API authorization. The API
  never enables wildcard CORS; same-origin hosting is the default deployment model.

## Independent WebUI and strict CORS

WebUI hosting is optional. A static site on another server can connect directly to this API:

```ini
V2RAYN_WEB_API_KEY=<your-management-key>
V2RAYN_WEB_ALLOWED_ORIGINS=https://webui.example.com,https://another.example.com
```

Unset/empty origins deny cross-origin API requests. Entries must be exact HTTP(S) origins
(scheme, host and port); paths, queries, fragments, credentials, empty list entries and wildcard
hosts are rejected at startup. Default ports are normalized. `https://good.example.com.evil.com`
does not match `https://good.example.com`. No forwarding headers are used to determine origin.

The origin guard rejects disallowed `Origin` requests before rate limiting, session authorization,
SSE-ticket consumption or mutations. Origin-less native clients and same-origin clients retain
their existing behavior and still require session Bearer authentication for protected endpoints.
Allowed-origin OPTIONS preflights complete before session authorization. CORS supports GET,
HEAD, POST, PUT, DELETE and OPTIONS, `Authorization` / `Content-Type`, and exposes only
`Content-Disposition` for downloads. It does not enable credentials, cookies, wildcard origins
or blanket private-network preflight bypasses. SSE tickets and `/api/events` use the same policy.
The allowlist grants browser access, not authentication; trust every allowed WebUI origin.

A same-origin browser behind a TLS/path-prefix reverse proxy retains REST access even when
Kestrel sees an internal HTTP URL: browser-controlled `Sec-Fetch-Site: same-origin` identifies
that case. It does not grant CORS response headers, setup access or internal health diagnostics.
Same-site/cross-site browser requests still require an exact allowed origin. Native clients
cannot gain privileges by forging this header: they already have origin-less access and still
must authenticate with the same Management Key/session model.

First-run setup remains **direct loopback + localhost/loopback Host + no forwarding headers**,
and additionally requires same-origin (or an origin-less native request). Allowed CORS origins
cannot initialize a Management Key. `/api/setup/status` remains public and reports
`setupAllowedFromThisRequest=false` for standalone clients. LAN/remote/supervised deployment
must configure `V2RAYN_WEB_API_KEY` before startup. The historical private-network setup branch
was removed to align with the local-only initialization policy.

For a *Backend-hosted* WebUI connecting to other Backends, the hosting Backend's CSP needs a
separate destination allowlist:

```ini
V2RAYN_WEB_UI_CONNECT_ORIGINS=http://127.0.0.1:5081,https://v2rayn-api.example.com
```

This uses the same strict origin validation but controls outgoing `connect-src`, not incoming
CORS. Default CSP and all other security headers are unchanged. The default hosted same-origin
UI requires neither setting. Standalone hosts control their own CSP and deployment defaults.

HTTPS public dashboards connecting to HTTP localhost/LAN remain subject to browser Local
Network Access permissions and mixed-content restrictions. CORS cannot override them; verify
REST and native EventSource in the target browser. Some browsers block HTTP LAN even with
permission; use same-origin hosting or an HTTPS API. Never expose an unauthenticated/wildcard
listener or disable the existing listener/Management Key policies to work around browser limits.

## Install any compatible WebUI

The API serves ordinary static files only. A compatible WebUI root needs an `index.html` and its
normal static files; no framework, asset directory name, manifest, or build tool is required.

The default root is `webui/` beside the `v2rayN.WebAPI` executable. For example, the reference
WebUI release ZIP has `index.html` and `assets/**` at its archive root:

```sh
mkdir -p webui
unzip -o v2rayN-WebUI.zip -d webui
```

Alternatively, set `V2RAYN_WEB_UI_PATH` in the process environment or executable-directory
`.env`:

```ini
V2RAYN_WEB_UI_PATH=/opt/v2rayn/webui
```

An unset value selects `<AppContext.BaseDirectory>/webui`. Absolute paths are used as-is;
relative paths are resolved from `AppContext.BaseDirectory`, never from the current working
directory. An explicitly empty value disables static WebUI hosting. Invalid paths, absent or
empty directories, and directories without `index.html` do not prevent the API from starting.

With a WebUI installed, existing static files are served directly and `GET`/`HEAD` routes without
a file extension fall back to `index.html` for SPA navigation. Missing file-like assets return
404. `/api/**` always belongs to the Backend: defined routes and API 404s can never fall through
to the WebUI.

Without a WebUI, all API, SSE, authentication, settings, subscription, routing, DNS, backup,
update, and runtime endpoints continue to work. `GET /` reports `v2rayN API is running. No
WebUI is installed.`; other non-API paths return 404.

The API package includes only a WebUI installation placeholder. It does not include the reference
Vue sources or a built UI. Backend self-update packages contain only the API executable and build
identity and never delete, overwrite, or recreate user-installed `webui/**` files.

## Prerelease update preferences

Update checks and installs default to each target's entry in `CheckPreReleaseCoreTypes`;
the API self-update target is `v2rayN.WebAPI`, independent of Desktop's `v2rayN` and Core targets.
A missing list or entry defaults to stable releases. An explicit `preRelease=true` or `false`
overrides the preference only for that request; batches use each target's own preference.

`GET /api/core-updates` returns `checkPreReleaseCoreTypes`. Pass this list to
`PUT /api/core-updates/settings` to save independent preferences (an empty list disables them).
Targets not exposed by this updater retain their stored preferences. For older clients that
omit the list, `preRelease` still applies to all exposed prerelease-capable targets, including
`v2rayN.WebAPI`; the response's legacy `preRelease` flag reflects the Web target's preference.

## Build and test the Backend

Requires the .NET 10 SDK. No Node.js or npm is used by the Backend build, tests, native publish,
Containerfile, or API release packaging.

```sh
bash Scripts/publish-native.sh linux-x64
bash Scripts/verify.sh
```

`Scripts/verify.sh` builds and tests the Backend, runs ServiceLib tests, checks startup/security,
and exercises API ZIP/self-update package boundaries. The independent `build-web.yml` workflow
runs these checks and native/container builds on relevant PRs and pushes to `master`; it can also
be dispatched manually or called by a release workflow. Release builds currently enable only
the tested `linux-x64` and `linux-arm64` native targets and both Linux container architectures.

`Scripts/test-native-update-identity.py <previous-publish-dir> <candidate-publish-dir>` additionally
exercises real native helper replacement and health-mismatch rollback with two distinct versions,
isolated data/ports and preserved third-party WebUI/Core markers. It never uses user configuration.

## Pre-merge product rename and update migration

The directory, project file, namespace, assembly/executable, embedded resource names, build
identity file/product and updater target are now `v2rayN.WebAPI`. Launcher/update helpers,
native/package scripts, Containerfile, solution registration, CI and tests use that identity.
The lock is `v2rayN.WebAPI.instance.lock`; the build identity is `v2rayN.WebAPI.build.json`.

**Old experimental `v2rayN.Web` installs require a one-time manual reinstall.** Stop the old
instance first (including its systemd/container supervisor), back up data and `.env`, install
the new full package, update ExecStart/container entrypoint, and restore the retained data/UI.
Do not run the two executables against the same data directory. There is no cross-name update
helper, executable alias or silent in-place migration. Old-product manifests/packages are not
accepted by the new updater; new `WebAPI → WebAPI` updates preserve the transactional helper,
health/PID/lock checks, atomic replacement/rollback and WebUI/Core/data boundaries.
Both package staging and the helper also check the Linux ELF64 header, declared RID's machine
architecture and program-table bounds before replacement; a script/truncated/wrong-ISA candidate
cannot reach shutdown/swap merely by carrying the correct JSON identity.

API-owned `SelectedCoreTypes` and `CheckPreReleaseCoreTypes` entries `v2rayN.Web` are normalized
to `v2rayN.WebAPI` at startup; legacy settings payloads are also normalized. Desktop/Core/hidden
preferences are preserved. This migrates preferences, not the executable or package identity.

Intentional compatibility names remain: `V2RAYN_WEB_*` configuration (including the Management
Key/UI-path variables), `WebVersion`/`WebCommit`/`WebBuildDate`/`WebRepository` build metadata,
`webVersion` and related status fields, `web.self-update`, `/api/web-updates`, native health header
names, `web-auth.json`, generic Web host/security class names, `webui/`, existing systemd unit /
install-path examples, progress/intent file names, workflow file names, all release ZIP names and
`web-update.json`. These are not stale executable assumptions.

## Releases and containers

Dispatch `release-web.yml` with a `release_tag` (`x.y.z`) to call `build-web.yml`, assemble and
validate the assets, then sign and upload them through the existing `upload-sign.yml` workflow.
The signing/upload jobs retain the upstream-only repository guard; forks can validate the build
artifacts without publishing. `build-web.yml` can also be dispatched with a tag for package-only
verification. `build-all.yml` dispatches the Web release independently of Linux Desktop;
`build-linux.yml` builds and releases only Desktop assets. Web release orchestration and artifact
names are RID-based, so additional tested native targets can be added separately from containers
and Desktop workflows. Windows Web packages are not enabled yet.

The API release provides architecture-specific full-install and app-only update ZIPs plus
`web-update.json`:

- `v2rayN-linux-64-web.zip` / `v2rayN-linux-arm64-web.zip` — API, runtime files, `.env.example`,
  Core bundle, and an optional empty `webui/README.txt` placeholder.
- `v2rayN-linux-64-web-update.zip` / `v2rayN-linux-arm64-web-update.zip` — API executable and
  build identity only; user data, Core files, credentials, and WebUI files are excluded.
- `web-update.json` — version, commit, runtime identifier, sizes, and SHA-256 digests.

The API ZIP and its self-update path are independent from the separately released
`v2rayN-WebUI.zip`. Updating the API preserves any installed first-party or third-party WebUI.

For Docker/Podman, the included `Containerfile` builds only the API and Core runtime. Mount a
compatible static site at `/app/webui` (read-only is sufficient) to have it served by the default
path; omit the mount for API-only operation. The Compose example requires a non-empty Management
Key.

See [`FEATURE-MAP.md`](FEATURE-MAP.md) for the current ServiceLib/API feature boundary.
