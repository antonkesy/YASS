# YAWSS — Yet Another Web Scanner Service

Base prompt / project specification for a coding agent. Read fully before writing code. Sections marked **DECIDED** are not up for debate; sections marked **OPEN** may be resolved by the agent with a one-line justification in `docs/decisions/`.

---

## 1. One-paragraph summary

YAWSS is a headless scanning service for a home server on Linux (bare metal, NixOS, Docker). A scanner is attached to the host (USB/SANE or network/eSCL). YAWSS exposes a small web UI where a user can see scanner readiness, start a scan that runs until the feeder is empty, review pages in a preview (rotate / delete / color mode, per page or for all), and save the result as a PDF into a configured directory — primarily the paperless-ngx consume folder. Everything is configured through a single file. Backend is .NET with **NAPS2.Sdk**; frontend is a **SvelteKit** mobile-first PWA. **There is no authentication**: the service is trusted-LAN only and must never be exposed to the internet (document this prominently in the README).

## 2. Guiding principle — DECIDED

**Use existing tools; write as little bespoke code as possible.** Before implementing anything, check whether NAPS2.Sdk, ASP.NET Core, ImageSharp, SvelteKit, or an established package already does it. Bespoke code is only for the domain (sessions, edits, config) and glue.

## 3. Stack — DECIDED

| Layer | Choice | Why / existing tool used |
|---|---|---|
| Scanner backend | **NAPS2.Sdk** (NuGet, 1.2.x) + **NAPS2.Images.ImageSharp** | ImageSharp context = headless, no GTK. SANE from host `sane-backends`; eSCL built in. Use NAPS2's own `PdfExporter`, `ImageExporter`, blank-page detection and deskew — do not reimplement. |
| Runtime | **.NET 8 LTS**, ASP.NET Core Minimal API, `PublishTrimmed`, `InvariantGlobalization=true` | |
| Transport — state, commands, events | **SignalR** hub over WebSocket with **MessagePack** protocol (`Microsoft.AspNetCore.SignalR.Protocols.MessagePack`, `@microsoft/signalr` + `@microsoft/signalr-protocol-msgpack` on the client) | One persistent bidirectional connection: server pushes scanner status, page-added, progress; client invokes `StartScan`, `ApplyEdits`, `Export`. Auto-reconnect, typed hub, .NET-native, no polling, no per-request overhead. Better fit than REST for a local single-user app. |
| Transport — binary | **Plain HTTP GET** for thumbnails/previews/PDF download | Browser cache + `ETag` + `Cache-Control: immutable` + `<img loading="lazy">` beats any custom channel for images. These are the only HTTP endpoints besides `/healthz`. |
| Config | **YAML** via `YamlDotNet`, file `yawss.yaml`, env overrides via ASP.NET's built-in configuration (`YAWSS__section__key`) | Bound to C# records with `IOptions<T>` + `ValidateDataAnnotations().ValidateOnStart()`. Fail fast, name the bad key. |
| Frontend | **SvelteKit** (`@sveltejs/adapter-static`, SPA fallback), TypeScript, **Tailwind**, **`@vite-pwa/sveltekit`** | Installable PWA = "export as app". No component library. |
| Shared types | Hub contract defined once in C# (`IYawssHub`, `IYawssClient`); TS types generated with **`TypedSignalR.Client.TypeScript`** in CI; drift fails the build | |
| Backend tests | **xUnit** + `WebApplicationFactory` + `NSubstitute` | Scanner behind `IScannerBackend`; `FakeScannerBackend` yields PNG fixtures. |
| Frontend tests | **Vitest** + `@testing-library/svelte`; **Playwright** E2E (desktop Chromium + Pixel 7 profile) against server in `--fake` mode | |
| Lint/format | `dotnet format` + analyzers (`TreatWarningsAsErrors`), `eslint`, `prettier`, `svelte-check`; **`pre-commit`** framework | |
| Commits/releases | **Conventional Commits** enforced by `conventional-pre-commit`; **`release-please`** for changelog + tags | |
| CI | **GitHub Actions** — `ci.yml`, `release.yml` (`nix build`) | |
| Packaging | **Nix flake** (dev shell, package, docker image via `dockerTools`, NixOS module `services.yawss`) + plain multi-stage `Dockerfile` | |
| Storage | Filesystem only; session = directory with `session.json` + `page-NNN.png` | |

**Not in v1:** auth of any kind, OCR (paperless does it), multi-scanner UI, cloud upload, barcode splitting, Tauri wrapper.

## 4. Repository layout — DECIDED

```
yawss/
├── flake.nix / flake.lock
├── Dockerfile
├── docker-compose.example.yml
├── yawss.example.yaml
├── .pre-commit-config.yaml
├── .editorconfig
├── .github/workflows/{ci,release}.yml
├── release-please-config.json
├── docs/decisions/ADR-000-template.md
├── src/
│   ├── Yawss.Core/        # domain records, IScannerBackend, IDocumentWriter, hub contracts (IYawssHub/IYawssClient)
│   ├── Yawss.Scanning/    # NAPS2 adapter
│   ├── Yawss.Export/      # PDF via NAPS2 PdfExporter, filename templating, atomic write to consume dir
│   ├── Yawss.Server/      # host: SignalR hub, HTTP image endpoints, static frontend, /healthz
│   └── Yawss.Fake/        # FakeScannerBackend + fixtures (tests and `--fake` dev mode)
├── tests/Yawss.{Core,Scanning,Export,Server}.Tests/
├── web/                   # SvelteKit
│   ├── src/lib/hub/       # generated hub types + one thin `hub.ts` wrapper (svelte store of connection state)
│   ├── src/lib/components/
│   ├── src/routes/        # /, /session/[id], /settings
│   └── tests/{unit,e2e}/
└── assets/logo/
```

Rule: `Yawss.Core` references nothing outside the BCL (plus `Microsoft.AspNetCore.SignalR.Core` abstractions for the hub interfaces).

## 5. Configuration — DECIDED

```yaml
server:
  listen: "0.0.0.0:8080"      # LAN bind; NO AUTH — never expose publicly
  base_path: "/"
scanner:
  driver: auto                # auto | sane | escl
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
  ui_editable: [source, dpi, color, page_size, blank_page_detection]
output:
  directory: /data/consume    # paperless consume dir
  filename_template: "{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf"
  pdf:
    compression: jpeg         # jpeg | lossless
    jpeg_quality: 75
  allow_download: true        # browser "save as" instead of server-side save
  on_success: keep_session    # keep_session | delete_session
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

## 6. Domain model — DECIDED

```
Session  { Id, CreatedAt, State: Idle|Scanning|Review|Exporting|Done|Failed, Pages: Page[], OptionsUsed }
Page     { Index, SourceFile, Rotation: 0|90|180|270, ColorOverride?, Deleted, IsBlank, Hash }
PageEdit { Target: All | int[], Op: Rotate(deg) | Delete | Restore | SetColor(mode) | Reorder(int[]) }
```

Edits are non-destructive and recorded in `session.json`; derived images are a pure function of `(source, edits)` cached on disk by hash.

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
Task<ExportResult> Export(string sessionId, ExportRequest req)   // mode: Save | Download
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
GET /export/{sessionId}/{token}.pdf                -> one-shot download URL returned by Export(mode: Download)
GET /                                              -> static SvelteKit build
```

Scan loop: iterate `ScanController.Scan(options)` until the enumerable ends (feeder empty) or `StopScan`; write each page to disk, emit `PageAdded` immediately, generate thumbnail in a bounded background queue, emit `PageUpdated`.

## 8. UI — DECIDED behaviour, OPEN styling

Routes: `/` (scanner status pill, big Scan button, recent sessions), `/session/[id]` (review), `/settings` (only `ui_editable` keys, stored in `localStorage`).

Review: responsive thumbnail grid (2 cols phone → 6 desktop); tap = multi-select; drag = reorder; toolbar acts on selection or "all" when nothing selected: rotate ⟲ ⟳, delete/restore, color mode. Tap → full preview with swipe. Blank pages pre-marked deleted with badge. Sticky bottom bar: page count, **Save to paperless**, Download (if `allow_download`), Discard. Edits apply optimistically (CSS rotate) and reconcile on `PageUpdated`.

Targets: first paint < 1 s over LAN; JS ≤ 150 kB gzipped; no image bytes held in JS beyond the current preview; all actions keyboard-reachable; touch targets ≥ 44 px; works without service worker.

## 9. Resource budget — DECIDED

Idle RSS ≤ 80 MB; 50-page 300 dpi color scan ≤ 400 MB peak; container image ≤ 200 MB. Never hold more than `max_concurrent_renders` decoded bitmaps in memory.

## 10. Testing — DECIDED

80 % line coverage gate on `Yawss.*` (excl. `Yawss.Fake`). Fake scenarios: N pages, blank at k, error after k, slow pages, feeder-empty-after-N. Export tests assert page count, rotation, deleted pages absent, atomic write (watch dir for partial files). Config tests: round-trip every key; invalid value names the key. Hub contract test: generated TS matches C#; drift fails CI. Playwright: desktop + mobile viewport, real server in `--fake` mode.

## 11. Commit plan — DECIDED

No monolithic commits. Each item below is one commit (or a small series) with a Conventional Commit message, each passing `pre-commit run --all-files` and all tests at that point. Order is the build order.

```
chore: init repo, editorconfig, gitignore, license, readme skeleton
chore(nix): add flake with dev shell (dotnet 8, node 22, pnpm, sane-backends)
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
```

Scopes: `core`, `fake`, `server`, `web`, `scanning`, `export`, `nix`, `docker`, `ci`. Fixes discovered while working get their own `fix(scope):` commit, not squashed into a feature.

## 12. Agent working rules

1. Follow the commit plan order. The app must be fully usable in `--fake` mode before `Yawss.Scanning` is written.
2. No new dependency without a one-line ADR in `docs/decisions/`. Prefer BCL / already-listed packages.
3. Never block a hub or request thread on scanning or rendering.
4. If the NAPS2 API differs from assumptions here, adapt the adapter, keep the domain model, and note it in an ADR.
5. Ask before: changing the config schema, adding a database, adding any auth.

## 13. Branding

**YAWSS** — Yet Another Web Scanner Service. Logo: an original triumphant "nailed it" character — flat-color figure pumping a fist while holding a scanned sheet; readable at 16 px, maskable at 512 px. SVG + PNG 192/512 + favicon. No recognisable actor, film still or existing meme image.

## 14. OPEN

- Thumbnail format WebP vs JPEG (WebP unless ImageSharp encode cost is high).
- Whether preview endpoint serves the edited image or the original + client-side CSS transform (prefer CSS for rotation, server for color mode).
