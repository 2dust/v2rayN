# v2rayN.WebAPI

`v2rayN.WebAPI` is a headless ASP.NET Core host for v2rayN's existing `ServiceLib`. It exposes
REST APIs, authenticated SSE/runtime events, setup and sessions, settings and options,
profiles/subscriptions, routing and DNS, backups, updates, and the existing Core runtime bridge.
It runs without a browser or desktop session. The API has no dependency on
Vue, React, Vite, Node.js, npm, or any frontend manifest.

The API only serves API endpoints; it does not host, inspect, or configure a WebUI. A WebUI,
including [Nozilla-X/v2rayN-WebUI](https://github.com/Nozilla-X/v2rayN-WebUI), is an independent
HTTP client with its own hosting and configuration.

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
`[::1]`), then complete first-run setup through a direct loopback API client.

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
  and API authorization remain Backend-owned and apply equally to every client.
- The API never enables wildcard CORS. Client hosting and configuration are independent.

## Browser/client origins and strict CORS

An independently hosted browser client can connect directly to this API:

```ini
V2RAYN_WEB_API_KEY=<your-management-key>
V2RAYN_WEB_ALLOWED_ORIGINS=https://client.example.com,https://another.example.com
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
The allowlist grants browser access, not authentication; trust every allowed browser/client origin.

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

HTTPS public dashboards connecting to HTTP localhost/LAN remain subject to browser Local
Network Access permissions and mixed-content restrictions. CORS cannot override them; verify
REST and native EventSource in the target browser. Some browsers block HTTP LAN even with
permission; use an HTTPS API or a same-origin reverse proxy. Never expose an unauthenticated/wildcard
listener or disable the existing listener/Management Key policies to work around browser limits.

Non-API paths, including `GET /`, return 404. The launcher opens the API health endpoint,
not a client application. API packages contain no frontend installation directory or placeholder.

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
and exercises API ZIP/self-update package boundaries and real different-layout native update,
rollback and invalid-executable rejection. The independent `build-web.yml` workflow
runs these checks and native/container builds on relevant PRs and pushes to `master`; it can also
be dispatched manually or called by a release workflow. Release builds currently enable only
the tested `linux-x64` and `linux-arm64` native targets and both Linux container architectures.

`Scripts/test-native-update-identity.py <previous-publish-dir> <candidate-publish-dir>` additionally
exercises real native helper replacement and health-mismatch rollback with two distinct versions,
isolated data/ports and preserved unrelated file/Core markers. It never uses user configuration.

### Native self-update and legacy helpers

The update worker runs from an owner-only, GUID-named copy of the current executable beside the
installation. This keeps its single-file bundle path unchanged during replacement and rollback:
.NET loads dependencies lazily, and replacing a running helper's own executable with a different
bundle layout can break later assembly loads. The outer helper waits for worker exit, propagates
its result, and then removes that copy. Configuration and data paths remain unchanged.

`Scripts/test-native-update-regression.sh <native-publish-directory>` deliberately changes the
candidate's bundle layout with test-only assembly metadata; version-only fixtures can conceal
this failure. It checks actual replacement, rollback, invalid-executable rejection and worker
cleanup, and is included in `Scripts/verify.sh`.

**Builds predating helper isolation need a one-time manual reinstall.** Their already-running
old helper cannot gain this fix from the candidate package. Stop the instance, back up the
executable, build identity, data and `.env`, and install the corrected API package. For a failed
legacy update, retain the previous-application backup and verify the restored application's
health before discarding it. Systemd/container deployments remain check-only and use redeploy.

## Pre-merge product rename and update migration

The directory, project file, namespace, assembly/executable, embedded resource names, build
identity file/product and updater target are now `v2rayN.WebAPI`. Launcher/update helpers,
native/package scripts, Containerfile, solution registration, CI and tests use that identity.
The lock is `v2rayN.WebAPI.instance.lock`; the build identity is `v2rayN.WebAPI.build.json`.

**Old experimental `v2rayN.Web` installs require a one-time manual reinstall.** Stop the old
instance first (including its systemd/container supervisor), back up data and `.env`, install
the new full package, update ExecStart/container entrypoint, and restore the retained data.
Do not run the two executables against the same data directory. There is no cross-name update
helper, executable alias or silent in-place migration. Old-product manifests/packages are not
accepted by the new updater; new `WebAPI → WebAPI` updates preserve the transactional helper,
health/PID/lock checks, atomic replacement/rollback and executable/Core/data boundaries.
Both package staging and the helper also check the Linux ELF64 header, declared RID's machine
architecture and program-table bounds before replacement; a script/truncated/wrong-ISA candidate
cannot reach shutdown/swap merely by carrying the correct JSON identity.

API-owned `SelectedCoreTypes` and `CheckPreReleaseCoreTypes` entries `v2rayN.Web` are normalized
to `v2rayN.WebAPI` at startup; legacy settings payloads are also normalized. Desktop/Core/hidden
preferences are preserved. This migrates preferences, not the executable or package identity.

Intentional compatibility names remain: `V2RAYN_WEB_*` configuration (including the Management
Key and allowed-origin variables), `WebVersion`/`WebCommit`/`WebBuildDate`/`WebRepository` build metadata,
`webVersion` and related status fields, `web.self-update`, `/api/web-updates`, native health header
names, `web-auth.json`, generic Web host/security class names, existing systemd unit /
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
  and Core bundle.
- `v2rayN-linux-64-web-update.zip` / `v2rayN-linux-arm64-web-update.zip` — API executable and
  build identity only; user data, Core files, credentials, and unrelated files are excluded.
- `web-update.json` — version, commit, runtime identifier, sizes, and SHA-256 digests.

For Docker/Podman, the included `Containerfile` builds only the API and Core runtime.
The Compose example requires a non-empty Management Key.

See [`FEATURE-MAP.md`](FEATURE-MAP.md) for the current ServiceLib/API feature boundary.
