# YASS — Yet Another Slop Scanner

Base prompt / project specification for a coding agent. Read fully before writing code. Sections marked **DECIDED** are not up for debate; sections marked **OPEN** may be resolved by the agent with a one-line justification in `docs/decisions/`.

---

## 1. One-paragraph summary

YASS is a headless scanning service for a home server on Linux (bare metal, NixOS, Docker). A scanner is attached to the host (USB/SANE or network/eSCL). YASS exposes a small web UI where a user can see scanner readiness, start a scan that runs until the feeder is empty, review pages in a preview (rotate / delete / color mode, per page or for all), and download the result as a PDF (or one image per page) to the device at hand — or, where `output.directory` is configured, save it into that one folder on the server (e.g. a paperless-ngx consume folder). The server writes documents nowhere else. Everything is configured through a single file. Backend is .NET with **NAPS2.Sdk**; frontend is a **SvelteKit** mobile-first PWA. **There is no authentication**: the service is trusted-LAN only and must never be exposed to the internet (document this prominently in the README).

The same code ships as two deployments (ADR-014):

1. **Server** (`Yass.Server`): the service above. It runs on the machine the scanner is attached to, a home server or just localhost, and any browser on the LAN uses it.
2. **Desktop** (`Yass.Desktop`): one native app for Linux, Windows and macOS. The same UI runs in a native webview inside the .NET process (Photino.NET, ADR-015). **It opens no listening socket, not even on loopback**: hub calls go over the webview's message bridge, and images and documents over a `yass://` scheme. The only network traffic is to an eSCL scanner, if one is used. The focus is speed: nothing is serialized onto a socket, and a saved document moves on disk without passing through JavaScript.

## 2. Guiding principle — DECIDED

**Use existing tools; write as little bespoke code as possible.** Before implementing anything, check whether NAPS2.Sdk, ASP.NET Core, ImageSharp, SvelteKit, or an established package already does it. Bespoke code is only for the domain (sessions, edits, config) and glue.

## 3. Stack — DECIDED

| Layer | Choice | Why / existing tool used |
|---|---|---|
| Scanner backend | **NAPS2.Sdk** (NuGet, 1.2.x) + **NAPS2.Images.ImageSharp** | ImageSharp context = headless, no GTK. SANE from host `sane-backends`; eSCL built in. Use NAPS2's own `PdfExporter`, `ImageExporter`, blank-page detection and deskew — do not reimplement. |
| Runtime | **.NET 10 LTS**, ASP.NET Core Minimal API, `PublishTrimmed`, `InvariantGlobalization=true` | |
| Transport — state, commands, events | **SignalR** hub over WebSocket with **MessagePack** protocol (`Microsoft.AspNetCore.SignalR.Protocols.MessagePack`, `@microsoft/signalr` + `@microsoft/signalr-protocol-msgpack` on the client) | One persistent bidirectional connection: server pushes scanner status, page-added, progress; client invokes `StartScan`, `ApplyEdits`, `Export`. Auto-reconnect, typed hub, .NET-native, no polling, no per-request overhead. Better fit than REST for a local single-user app. |
| Transport — binary | **Plain HTTP GET** for thumbnails/previews/PDF download | Browser cache + `ETag` + `Cache-Control: immutable` + `<img loading="lazy">` beats any custom channel for images. These are the only HTTP endpoints besides `/healthz`. |
| Config | **YAML** via `YamlDotNet`, file `yass.yaml`, env overrides via ASP.NET's built-in configuration (`YASS__section__key`) | Bound to C# records with `IOptions<T>` + `ValidateDataAnnotations().ValidateOnStart()`. Fail fast, name the bad key. |
| Frontend | **SvelteKit** (`@sveltejs/adapter-static`, SPA fallback), TypeScript, **Tailwind**, **`@vite-pwa/sveltekit`** | Installable PWA = "export as app". No component library. |
| Shared types | Hub contract defined once in C# (`IYassHub`, `IYassClient`); TS types generated with **`TypedSignalR.Client.TypeScript`** in CI; drift fails the build | |
| Backend tests | **xUnit** + `WebApplicationFactory` | Scanner behind `IScannerBackend`; `FakeScannerBackend` yields PNG fixtures. |
| Frontend tests | **Vitest** + `@testing-library/svelte`; **Playwright** E2E (desktop Chromium + Pixel 7 profile) against server in `--fake` mode | |
| Lint/format | `dotnet format` + analyzers (`TreatWarningsAsErrors`), `eslint`, `prettier`, `svelte-check`; **`pre-commit`** framework | |
| Commits/releases | **Conventional Commits** enforced by `conventional-pre-commit`; **`release-please`** for changelog + tags | |
| CI | **GitHub Actions** — `ci.yml`, `release.yml` (`nix build`) | |
| Desktop shell | **Photino.NET** 4.x (ADR-015): WebKitGTK 4.1 on Linux, WebView2 on Windows, WKWebView on macOS | Hosts the same `web/` build in-process. `window.external.sendMessage` carries hub calls as JSON; a custom `yass://` scheme serves the app, `/img` and `/export`. Native save and folder dialogs. |
| Packaging | **Nix flake** (dev shell, server and desktop packages, docker image via `dockerTools`, NixOS module `services.yass`) + plain multi-stage `Dockerfile`; desktop zips for linux-x64, win-x64 and osx-arm64 from CI | |
| Storage | Filesystem only; session = directory with `session.json` + `page-NNN.png` | |

**Not in v1:** auth of any kind, OCR (paperless does it), multi-scanner UI, cloud upload, barcode splitting, TWAIN, signed or notarized desktop installers.

## 4. Repository layout — DECIDED

```
yass/
├── flake.nix / flake.lock
├── Dockerfile
├── docker-compose.example.yml
├── yass.example.yaml
├── .pre-commit-config.yaml
├── .editorconfig
├── .github/workflows/{ci,release}.yml
├── release-please-config.json
├── docs/decisions/ADR-000-template.md
├── src/
│   ├── Yass.Core/        # domain records, IScannerBackend, IDocumentWriter, hub contracts (IYassHub/IYassClient)
│   ├── Yass.Scanning/    # NAPS2 adapter
│   ├── Yass.Export/      # PDF and images via NAPS2 exporters, filename templating
│   ├── Yass.App/         # host-agnostic app: config, sessions, scan loop, rendering, export, YassApi, IClientNotifier
│   ├── Yass.Server/      # server host: SignalR hub, HTTP image endpoints, static frontend, /healthz
│   ├── Yass.Desktop/     # desktop host: Photino window, yass:// scheme, JSON message bridge
│   └── Yass.Fake/        # FakeScannerBackend + fixtures (tests and `--fake` dev mode)
├── tests/Yass.{Core,Scanning,Export,Server,Desktop}.Tests/
├── web/                   # SvelteKit
│   ├── src/lib/hub/       # generated hub types + one thin `hub.ts` wrapper (svelte store of connection state)
│   ├── src/lib/components/
│   ├── src/routes/        # /, /session/[id], /settings
│   └── tests/{unit,e2e}/
└── assets/logo/
```

Rule: `Yass.Core` references nothing outside the BCL (plus `Microsoft.AspNetCore.SignalR.Core` abstractions for the hub interfaces). `Yass.App` references no ASP.NET Core or SignalR, only `Microsoft.Extensions.*`; the hosts hold the transports and nothing else.

## 5. Configuration — DECIDED

```yaml
server:
  listen: "0.0.0.0:8080"      # LAN bind; NO AUTH — never expose publicly
  base_path: "/"
scanner:
  driver: auto                # auto | sane | escl | wia  (auto: SANE on Linux, WIA on Windows, eSCL on macOS)
  device: ""                  # NAPS2 device id or substring; empty = first found
  poll_interval_seconds: 5
  defaults:
    source: feeder            # flatbed | feeder | duplex
    dpi: 300
    color: grayscale          # bw | grayscale | color
    page_size: a4
    brightness: 0
    contrast: 0
    blank_page_detection: true
    blank_page_threshold: 0.02
    auto_deskew: false
    device: ""                # exact device id; empty = whatever scanner.device resolves to
  ui_editable: []             # deprecated, ignored: every default above can be changed from the UI
output:                       # how a document is written, and the one server folder it may go to
  directory: ""               # absolute; empty = off. Browsers opt in per browser in Settings
  pdf:
    compression: jpeg         # jpeg | lossless
    jpeg_quality: 75
processing:
  max_concurrent_renders: 2
  thumbnail_width: 320
  preview_max_width: 1600
sessions:
  directory: /data/sessions
  retention_hours: 24
logging:
  level: info
  format: text                # text | json
```

`ReloadConfig` hub method re-reads non-`server` keys.

The desktop app reads the same file from the per-user config folder (`yass/yass.yaml` under `Environment.SpecialFolder.ApplicationData`), and the file may be absent. It ignores `server`, and keeps sessions under `LocalApplicationData/yass/sessions` unless `sessions.directory` is set.

## 6. Domain model — DECIDED

```
Session  { Id, CreatedAt, State: Idle|Scanning|Review|Exporting|Done|Failed, Pages: Page[], OptionsUsed }
Page     { Index, SourceFile, Rotation: 0|90|180|270, Filter: Original|Enhance|Grayscale|Bw, Crop?,
           StartsDocument, Deleted, IsBlank, SourceHash, Hash,
           Adjust? { Brightness -100..100, Contrast -100..100, Sharpen 0..100 },
           Cleanup: None | Despeckle | AutoLevels (flags), Skew: -15..15° in 0.1° }
PageEdit { Targets?: int[] (null = all), Op: Rotate(deg) | Delete | Restore | SetFilter(filter) | Reorder(int[])
           | SetCrop(box?) | SetSplit(bool) | SetAdjust(adjust?) | AddCleanup(flags) | RemoveCleanup(flags) | SetSkew(deg) }
```

Edits are non-destructive and recorded in `session.json`; derived images are a pure function of `(source, edits)` cached on disk by hash. One `RenderSpec` feeds both the hash and the pixels, so nothing reaches the image without reaching the hash; edits added later are appended to the hash key only when set, so older pages keep their hashes.

Render order (`PagePipeline`): crop (on the scan as it came) → quarter turns → skew (rotated, then cropped back to the page's own proportions, so no corners show) → auto-levels → Enhance filter → brightness, contrast → despeckle → sharpen → grayscale / B&W threshold.

## 7. Hub contract + HTTP — DECIDED

**SignalR hub `/hub`** (MessagePack):

```
// client -> server
Task<SessionDto>   StartScan(ScanOptionsDto? overrides)
Task               StopScan(string sessionId)
Task<SessionDto>   GetSession(string sessionId)
Task<SessionDto[]> ListSessions()
Task               DeleteSession(string sessionId)
Task<SessionDto>   ApplyEdits(string sessionId, PageEdit[] edits)
Task<int>          AutoCrop(string sessionId, int[]? targets)     // finds the scanner's dark frame, applies it as SetCrop; returns pages cropped
Task<ExportResult> Export(string sessionId, ExportRequest req)   // builds the document(s); one-shot download URLs, or with ToServer, files in output.directory
Task<string>       GetServerFolder()                             // output.directory, or "" when saving to the server is off
Task<ScannerStatusDto> GetScannerStatus()
Task<ScanOptionsDto>   GetEffectiveOptions()
Task               ReloadConfig()

// server -> client
ScannerStatusChanged(ScannerStatusDto)
SessionChanged(SessionDto)              // state transitions
PageAdded(string sessionId, PageDto)    // emitted before thumbnail exists
PageUpdated(string sessionId, PageDto)  // after edit / thumbnail ready (carries new hash)
ExportProgress(string sessionId, int percent)
```

**HTTP:**

```
GET /healthz
GET /img/{sessionId}/{pageIndex}/thumb?h={hash}    -> webp, ETag=hash, Cache-Control: public, max-age=31536000, immutable
GET /img/{sessionId}/{pageIndex}/preview?h={hash}
GET /img/{sessionId}/{pageIndex}/frame?h={hash}      -> preview with every edit but crop and skew, for drawing a crop over
GET /export/{sessionId}/{token}.pdf                -> one-shot download URL returned by Export
GET /                                              -> static SvelteKit build
```

**Desktop transport** (same contract, no socket):

```
JS -> host   {"id": n, "method": "StartScan", "args": [...]}         // any IYassHub method, plus the desktop-only
host -> JS   {"id": n, "result": ...} | {"id": n, "error": "msg"}     // PickSavePath, PickFolder, SaveExport
host -> JS   {"event": "PageAdded", "args": [...]}                    // every IYassClient push
yass://app/...                                                          -> the static build, /img/... and /export/... as above
```

Scan loop: iterate `ScanController.Scan(options)` until the enumerable ends (feeder empty) or `StopScan`; write each page to disk, emit `PageAdded` immediately, generate thumbnail in a bounded background queue, emit `PageUpdated`.

## 8. UI — DECIDED behaviour, OPEN styling

Routes: `/` (scanner picker, presets, big Scan button, recent sessions), `/session/[id]` (review, scanner status pill in the header), `/settings` (every scan option, stored in `localStorage`).

The UI comes first: it can change every scan option, and `yass.yaml` only supplies the defaults (`scanner.defaults`). Nothing in the config hides a control; `scanner.ui_editable` is accepted for old files and ignored. The scanner is chosen on `/`, not in `/settings`: the picker shows whenever the server lists at least one scanner, with `scanner.defaults.device` as the default, and presets belong to whichever scanner is chosen unless saved as available for all scanners. `/settings` lists every scanner the server lists now, this browser has seen before or a past scan ran on; each can be renamed or hidden from the picker (per browser, in `localStorage`) and shows its driver, address and usage. One no longer listed stays as a faded ghost until forgotten.

Review: page strip (a row above the page on a phone, a column beside it on desktop); tap a thumbnail = show that page on the stage, its checkbox = multi-select; drag = reorder; toolbar acts on the selection, or the page on the stage when nothing is selected: rotate ⟲ ⟳, Adjust (brightness, contrast, sharpen with a live CSS preview), Crop menu (Crop…, Straighten… ±15° over a grid, Auto-crop borders), Filter menu (Original / Enhance / Grayscale / B&W, plus Despeckle and Auto-levels clean-ups), rescan, delete/restore. While a scan runs there is no edit bar. Tap → full preview with swipe, its own edit bar (Adjust, Crop, Straighten, Filter) for the page on screen, and previews preloaded nearest-first around the page in view. Blank pages pre-marked deleted with badge. Sticky bottom bar: page count, **Save as…** (browser download, through the save dialog where there is one), **Save to server** (only when `output.directory` is set and this browser switched it on in Settings; atomic write, never overwrites, `-2`, `-3`… on a clash), Discard. Edits apply optimistically (CSS rotate) and reconcile on `PageUpdated`.

Targets: first paint < 1 s over LAN; JS ≤ 150 kB gzipped; no image bytes held in JS beyond the current preview; all actions keyboard-reachable; touch targets ≥ 44 px; works without service worker.

## 9. Resource budget — DECIDED

Idle RSS ≤ 80 MB; 50-page 300 dpi color scan ≤ 400 MB peak; container image ≤ 200 MB. Desktop: the same bounds for the .NET process (the webview's own processes excluded), window up in < 1 s on a warm start. Never hold more than `max_concurrent_renders` decoded bitmaps in memory.

## 10. Testing — DECIDED

80 % line coverage gate on `Yass.*` (excl. `Yass.Fake`). Fake scenarios: N pages, blank at k, error after k, slow pages, feeder-empty-after-N. Export tests assert page count, rotation, deleted pages absent, one-shot download URLs. Config tests: round-trip every key; invalid value names the key. Hub contract test: generated TS matches C#; drift fails CI. Playwright: desktop + mobile viewport, real server in `--fake` mode.

## 11. Commit plan — DECIDED

No monolithic commits. Each item below is one commit (or a small series) with a Conventional Commit message, each passing `pre-commit run --all-files` and all tests at that point. Order is the build order.

```
chore: init repo, editorconfig, gitignore, license, readme skeleton
chore(nix): add flake with dev shell (dotnet 10, node 24, pnpm, sane-backends)
chore: add pre-commit config with dotnet, node, nix, yaml hooks
chore(dotnet): create solution and empty projects with analyzers and warnaserror
feat(core): add domain records for session, page and page edits
feat(core): add config records with validation attributes
test(core): add config round-trip and validation tests
feat(core): define IScannerBackend, IDocumentWriter and hub contracts
feat(fake): add FakeScannerBackend with scripted scenarios and png fixtures
feat(server): bootstrap host, config binding, /healthz and static hosting
feat(server): add SignalR hub with MessagePack and scanner status broadcast
feat(server): implement session lifecycle and scan loop on fake backend
feat(server): add page edit handling with non-destructive edit state
feat(server): add thumbnail and preview rendering with disk cache and immutable http endpoints
test(server): add hub integration tests via WebApplicationFactory
chore(web): scaffold SvelteKit with static adapter, tailwind and pwa plugin
chore(web): add hub type generation and contract check script
feat(web): add hub connection store with reconnect
feat(web): add home route with scanner status and scan button
feat(web): add session review grid with selection and toolbar
feat(web): add full-page preview with swipe navigation
feat(web): add drag reorder and blank page badges
feat(web): add settings route backed by localStorage
feat(web): add save and download flow with progress
test(web): add vitest unit tests for components and stores
test(web): add playwright e2e for scan-review-save on desktop and mobile
feat(scanning): implement Naps2ScannerBackend with device discovery and option mapping
feat(scanning): add blank page detection and deskew via naps2
test(scanning): add option mapping tests
feat(export): assemble pdf via naps2 PdfExporter with applied edits
feat(export): add filename templating and atomic write to consume directory
test(export): add pdf assembly and atomic write tests
ci: add github actions workflow for lint, test, e2e and coverage gate
ci: add release-please and multi-arch image publish
chore(docker): add multi-stage Dockerfile and compose example
feat(nix): add package, docker image and NixOS module
docs: write readme with quickstart, config reference and lan-only warning
docs: add desktop deployment to spec
refactor(server): broadcast through IClientNotifier
refactor(server): move hub logic into YassApi
refactor(app): extract Yass.App project
feat(desktop): photino host serving the app over yass://
feat(desktop): json message bridge for hub calls and events
test(desktop): bridge dispatcher round-trip tests
feat(web): message-bridge transport selected at runtime
feat(web): native save dialogs on desktop
feat(scanning): wia driver and escl as the macos default
perf(desktop): local-first defaults
feat(nix): yass-desktop package
ci: build and release desktop for linux, windows and macos
docs: readme desktop section
```

Scopes: `core`, `app`, `fake`, `server`, `desktop`, `web`, `scanning`, `export`, `nix`, `docker`, `ci`. Fixes discovered while working get their own `fix(scope):` commit, not squashed into a feature.

## 12. Agent working rules

1. Follow the commit plan order. The app must be fully usable in `--fake` mode before `Yass.Scanning` is written.
2. No new dependency without a one-line ADR in `docs/decisions/`. Prefer BCL / already-listed packages.
3. Never block a hub or request thread on scanning or rendering.
4. If the NAPS2 API differs from assumptions here, adapt the adapter, keep the domain model, and note it in an ADR.
5. Ask before: changing the config schema, adding a database, adding any auth.

## 13. Branding

**YASS** — Yet Another Slop Scanner. Logo: an original triumphant "nailed it" character — flat-color figure pumping a fist while holding a scanned sheet; readable at 16 px, maskable at 512 px. SVG + PNG 192/512 + favicon. No recognisable actor, film still or existing meme image.

## 14. OPEN

- Thumbnail format WebP vs JPEG (WebP unless ImageSharp encode cost is high).
- Whether preview endpoint serves the edited image or the original + client-side CSS transform (prefer CSS for rotation, server for color mode).
