# v2rayN Web API

`v2rayN.Web` is a headless ASP.NET Core host for v2rayN's existing `ServiceLib`. It exposes
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
./v2rayN.Web --foreground --no-open
```

The default listener is `http://0.0.0.0:5080`. Protect it with a firewall/VPN or an HTTPS reverse
proxy; do not expose it directly to the public Internet. Systemd and container deployments
require `V2RAYN_WEB_API_KEY` to be set before startup. For an interactive native install without
an environment key, first-run setup remains controlled by the Backend's existing setup policy.

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
  does not enable wildcard CORS; same-origin hosting is the default deployment model.

## Install any compatible WebUI

The API serves ordinary static files only. A compatible WebUI root needs an `index.html` and its
normal static files; no framework, asset directory name, manifest, or build tool is required.

The default root is `webui/` beside the `v2rayN.Web` executable. For example, the reference
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

## Build and test the Backend

Requires the .NET 10 SDK. No Node.js or npm is used by the Backend build, tests, native publish,
Containerfile, or API release packaging.

```sh
bash Scripts/publish-native.sh linux-x64
bash Scripts/verify.sh
```

`Scripts/verify.sh` builds and tests the Backend, runs ServiceLib tests, checks startup/security,
and exercises API ZIP/self-update package boundaries. Release CI additionally builds native
`linux-x64` and `linux-arm64` packages and both container architectures.

## Releases and containers

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
