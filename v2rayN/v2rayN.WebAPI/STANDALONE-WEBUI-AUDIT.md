# Standalone WebUI / WebAPI rename audit

Historical verification record for the pre-cleanup baseline, not deployment instructions.
The WebAPI now only serves API endpoints. Client hosting/configuration are independent;
the former static hosting, installation placeholder and outgoing client CSP configuration
have been removed. Browser/client origins continue to use the generic CORS allowlist.

## API-only cleanup verification (2026-10-07)

- Removed static-file hosting, client-directory resolution/existence checks, SPA fallback,
  client installation messages, client CSP destinations, the static-hosting capability,
  installation placeholders and all client-specific packaging/deployment/test rules.
- Launcher URLs and localized messages now identify the API health endpoint, not a client page.
  Existing launcher/process/lock behavior is retained.
- Preserved the generic allowed-origin configuration, origin guard and CORS, Management Key,
  sessions, SSE tickets, listener/setup policy, rate limits, body limits and security headers.
  ServiceLib, Desktop, API implementations and runtime/update-helper logic are unchanged.
- Whole-repository tracked-source search: remaining client mentions are independent-client
  documentation or historical browser records. The root ignore template's commented static
  directory example is unrelated to WebAPI. The only `MapFallback` returns API 404 JSON under
  `/api/**`; it is not a page fallback.
- `Scripts/verify.sh`: passed; WebAPI **325/325**, ServiceLib **126/126**, no skipped tests,
  build **0 warnings / 0 errors**, native startup security and package/runtime smoke passed.
  The eleven removed tests exercised only the deleted static host/options/client CSP.
- Native linux-arm64 cross-publish passed; ARM64 runtime execution is not claimed.
- Real multi-stage linux/amd64 Containerfile build and UID 10001 container smoke passed:
  non-API GET/HEAD return 404, allowed/denied browser origins, session authentication,
  SSE ticket creation and remote setup restrictions remain functional. No image push.
- Extra native helper smoke between API-only **0.0.1-review → 0.0.2-review** builds passed
  replacement, rollback and invalid-executable rejection, preserving unrelated file/Core markers.
  **Limitation:** updating a pre-cleanup build to the API-only build failed after swap with
  `System.IO.Pipes` assembly loading errors (helper exit 2). An API-only review-to-dev artifact
  pair also failed; pre-cleanup-to-pre-cleanup controls passed, including matching build metadata.
  These results do not establish the cause or rule out a cleanup-related regression. The helper
  source is unchanged and no updater fix is included in this hosting-only cleanup; cross-build
  native self-update must not be described as fully verified.
- No tag or Release is part of this cleanup. Earlier results below describe the historical
  pre-cleanup baseline, not a rerun of the frontend/browser matrix.

Verification date: 2026-10-07. Backend verification baseline: `web-api-pr`; delivery
branch: `web-api` (chosen after review). Reference WebUI: the independent
`Nozilla-X/v2rayN-WebUI` repository, delivered on `main`. Existing remote `web` and
`web-api-pr` branches are not changed. No tag or Release was created.

## 1. Backend selection and session isolation

- Default is unchanged same-origin `/api/**`; independent sites set a deployment default in
  `webui-config.js` or users select an absolute HTTP(S) Backend on the unsigned-in screen.
- A single API-only URL resolver normalizes scheme/host/default port/trailing slash and composes
  proxy prefixes. REST, public login/setup/health, downloads, SSE tickets and EventSource use it.
  Absolute/unrelated routes cannot be passed to the authenticated client. Static assets remain
  on the WebUI origin. A source regression test rejects scattered `fetch` / EventSource calls.
- Applying a different Backend synchronously closes SSE, aborts pending API requests, clears
  credential drafts/dialogs/runtime state and fences stale responses/events. SSE log generation
  is reset for the new Backend. A bounded best-effort logout uses only the captured old endpoint
  and old session, never the new Backend.
- Session storage identity is the normalized endpoint including its path prefix. Old unscoped
  `v2rayn-web-token` values are discarded. Management Keys remain input/login/setup data, never
  browser-persisted preferences. REST credentials are Bearer sessions; cookies/redirect forwarding
  are not introduced. Standalone API contact starts only after Test connection or Sign in.

## 2. CORS and initialization boundaries

- `V2RAYN_WEB_ALLOWED_ORIGINS` defaults to empty. HTTP(S) origin-only values are validated at
  startup; wildcard hosts, credentials, non-root paths (including normalized dot-segment paths),
  queries/fragments and empty comma-separated entries are rejected without echoing secret input.
- Incoming cross-origin browser access matches the exact serialized scheme/host/port. An origin
  guard runs before auth/rate-limit/ticket consumption; allowed OPTIONS completes before session
  auth. Methods are GET/HEAD/POST/PUT/DELETE/OPTIONS, request headers Authorization/Content-Type,
  and exposed response header Content-Disposition. There is no AllowAnyOrigin, credential CORS,
  cookie authentication or blanket private-network preflight exemption.
- Same-origin browsers behind HTTPS reverse proxies retain ordinary REST through their
  browser-controlled Fetch Metadata. This grants no cross-origin response headers, setup access
  or private health diagnostics; native clients still require the same auth model.
- Setup is direct loopback + local Host + no forwarding headers + same-origin/origin-less native
  request. The former private-network setup branch is removed to match the local-only policy.
  Even an allowed browser/client origin cannot initialize a key; setup/status explains that restriction.
- Existing Management Key verifier/session expiry/login limit/SSE ticket/body limit/listener
  exposure/DNS-rebinding protections remain. API security headers retain their default CSP.

## 3. Actual browser verification

Built Vue artifact + real isolated Kestrel Backend. Actual login/Bearer data, native EventSource
and a real backup download were tested, not API/SSE fixtures. Chrome for Testing
**153.0.8010.12** and Playwright Firefox **155.0** used fresh profiles.

| Scenario | Chrome REST / SSE / download | Firefox REST / SSE / download |
|---|---|---|
| HTTPS same-origin reverse proxy, empty CORS allowlist | Pass | Pass |
| Independent local HTTP WebUI → loopback HTTP API | Pass | Pass |
| Independent UI → HTTPS API with reverse-proxy path prefix | Pass | Pass |
| Independent local HTTPS UI → loopback HTTP API | Pass | Pass |
| Independent local HTTPS UI → LAN HTTP API | Pass | Blocked as mixed active content |
| Public HTTPS document → loopback HTTP API | Initially permission-denied; after LNA grant, pass | Pass |
| Public HTTPS document → LAN HTTP API | Initially permission-denied; after LNA grant, pass | Blocked as mixed active content, including after permission grant |

Public-origin testing navigated a real public HTTPS document, verified Chrome's **Public** source
address space/public remote address, then injected only the local static UI into the existing
DOM. No public server was modified/deployed and no API request was intercepted. This tests the
actual browser security boundary without publishing a test site. Initial synthetic-document and
proxied attempts reported **Unknown**, so those runs were not used as LNA proof. Local HTTPS used
an isolated self-signed test certificate; no system/browser trust or security settings were changed.

Standalone cases made **zero API requests before an explicit action**. Chrome's headless permission
grant was controlled through browser automation after observing a genuine loopback/local LNA
denial; this is not a manual confirmation of a production browser's permission dialog. Firefox's
LAN refusal is a browser limitation, not an API/CORS workaround opportunity. Both browsers passed
the path-prefix case with HTTPS API transport. No DNS-to-private-domain feature was added.

Reproduction: frontend `Tests/browser/backendConnection.mjs`. Browser differences must be
rechecked for other versions, enterprise policies, actual hosting CSP and network arrangements.

## 4. Product identity versus retained contracts

Changed to `v2rayN.WebAPI`: project directory/csproj/root namespace/assembly/executable,
solution registration, embedded resource names, friend test assembly, updater target, native
helper executable assumptions, process replacement, `v2rayN.WebAPI.instance.lock`,
`v2rayN.WebAPI.build.json`/product, native/package scripts, Containerfile/CI paths and visible
Backend launcher/update text. No generic Web host/security/listener classes were renamed.

Intentionally retained: API/security/runtime `V2RAYN_WEB_*` settings, WebVersion/WebCommit/WebBuildDate/
WebRepository build metadata, `webVersion` and related JSON fields, `web.self-update`,
`/api/web-updates`, native health header names, `web-auth.json`, operational systemd
unit/install-path examples, progress/intent filenames, workflow filenames, all `*-web.zip` /
`*-web-update.zip` release names and `web-update.json`. The frontend consumes the updater's
advertised name, retaining compatibility with historical APIs.

## 5. Self-update migration and extra findings

- Old experimental `v2rayN.Web` → `v2rayN.WebAPI` is a **one-time manual reinstall**, not an
  executable alias or cross-name helper. Stop the old instance/supervisor first, back up and
  preserve data/.env, then install and update ExecStart. Never run both against one data scope.
- Only API-owned update selection/prerelease names migrate old → new. Desktop/Core/hidden
  preferences are retained; old settings payload names are normalized. Old-product packages
  are rejected rather than silently installed under the new executable name.
- Actual native WebAPI **0.0.1-review → 0.0.2-review** helper replacement passed; an actual
  native candidate with deliberate version-health mismatch rolled back successfully. Version,
  owner PID/lock, atomic swap and unrelated file/Core markers were checked.
- An early handcrafted shell candidate bypassing the normal release staging path caused helper
  SIGBUS. The audit added ELF64/RID/program-table sanity checks in staging **and** the helper.
  A new actual-native helper rejection scenario now rejects that candidate before swap and
  restarts the unchanged healthy app. Legitimate replacement and health-mismatch rollback still pass.
- Native/container build inputs exclude SDK/test reports and
  private generated config/log/temp/backup directories. Nothing generated or credential-bearing
  belongs in either commit.

These tests do not publish a Release or download/install a real newly published GitHub Release.
Trusted release URL/manifest/SHA/RID/archive rules have automated coverage; native helper behavior
uses actual artifacts. ARM64 cross-publish/package boundaries are checked, but no ARM64 runtime
or ARM64 container was executed on this x64 machine.

## 6. Executed verification

- Backend tests: **336 passed**, no skipped tests; build **0 warnings / 0 errors**.
- ServiceLib regression: **126 passed**, no ServiceLib/WPF/Avalonia source diff.
- `Scripts/verify.sh`: passed restore/build/backend/ServiceLib/native x64 publish/startup exposure
  and malformed-env checks/full+update ZIP boundaries/both RID package fixtures/manifest identity
  tamper rejection/API-only ZIP runtime/graceful-shutdown/no-residual-process smoke.
- Native linux-arm64 cross-publish: passed. Native real helper replacement, rollback and invalid
  executable rejection: passed, preserving unrelated file/Core markers.
- Real multi-stage Containerfile linux/amd64 build: passed. Non-root UID 10001 container health,
  allowed-origin login/Bearer status, API-only mode and absence of legacy executable/UI/SDK/test
  artifacts: passed; temporary container was stopped/removed. No image push.
- Frontend `npm test`: **117 passed** after the login-card placement refinement; `npm run typecheck`, locale parity and `npm run build`: passed.
- Real Chrome/Firefox matrix: the 16 cases above; supported cases pass REST/SSE/download, two
  Firefox HTTP-LAN cases record expected browser-policy denial with human-readable diagnostics.
- Review proxy: isolated API/UI on loopback **5080**, HTTP/SOCKS on loopback **1145**; both proxy
  requests returned HTTP 200 through the unchanged existing local upstream. No user subscription
  or private profile was copied. Review data/Management Key remain outside Git.
- Per user review, API address/testing fields were moved into the original login card, above
  the Management Key (also retained within existing setup cards). Actual 990px/390px browser
  checks passed: one card, no nested forms/overflow, and Test/Enter do not submit the login form.
  Static WebUI assets were updated without restarting the API or proxy.
- Whitespace and full scope/rename/contract/secret/generated-artifact diff audit: performed before
  commit. User acceptance gates commit/push; no tag or Release is authorized.

Earlier failing iterations (rather than final pass claims): unscoped-token fixtures needed scope
updates; 401 cleanup was initially misclassified as cancellation; SSE wrapper changed a static
regex fixture; local test installs initially omitted required bin/; Firefox cleanup needed the
actual host shutdown budget; public DOM injection needed classic config before Vite's deferred
module and removal of the public page's old favicon. Each was corrected and rerun. The unsafe
shell candidate finding and its subsequent guard/rejection test are recorded above.
