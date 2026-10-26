# YASS — Yet Another Slop Scanner

What YASS is, the decisions that shape it, and the constraints behind them. The code is the
reference for details such as hub methods, config keys and UI layout; this file is for what stays
true while those change.

## 1. Summary

YASS scans documents: press Scan, let the feeder run empty, review the pages (rotate, crop,
straighten, adjust, filter, reorder, delete), and save a PDF or one image per page. It ships the
same code as two hosts:

1. **Desktop** (`Yass.Desktop`): a native app for Linux, Windows and macOS. The UI runs in the
   platform webview inside the .NET process. **It opens no listening socket, not even on
   loopback**: hub calls go over the webview's message bridge, and images and documents over a
   custom scheme. The only network traffic is to an eSCL scanner, if one is used.
2. **Server** (`Yass.Server`): the same app as a web service on the machine the scanner is
   attached to (bare metal, NixOS, Docker), used from any browser on the LAN. It downloads to the
   device at hand, or saves into the one folder set as `output.directory` (e.g. a paperless-ngx
   consume folder), and writes documents nowhere else. **There is no authentication**: it is
   trusted-LAN only and must never be exposed to the internet. The README says so prominently.

Everything is configured through one optional file, `yass.yaml` (see `yass.example.yaml`).

**Out of scope:** auth of any kind, OCR (paperless does it), cloud upload, barcode splitting,
TWAIN, signed or notarised desktop packages.

## 2. Guiding principle

**Use existing tools; write as little bespoke code as possible.** Before implementing anything,
check whether NAPS2.Sdk, .NET, ImageSharp, SvelteKit or an established package already does it.
Bespoke code is for the domain (sessions, edits, config) and glue.

## 3. Stack

| Layer | Choice |
|---|---|
| Scanning and PDF | **NAPS2.Sdk** + `NAPS2.Images.ImageSharp` (headless, no GTK). SANE from the host's `sane-backends`, WIA on Windows, eSCL built in. |
| Runtime | **.NET 10 LTS**, `InvariantGlobalization`; the server is ASP.NET Core Minimal API with `PublishTrimmed`. |
| Images | **ImageSharp** renders thumbnails, previews (WebP) and export pages. |
| Transport | Server: **SignalR** over WebSocket with **MessagePack**, plus plain HTTP GET for images and downloads. Desktop: a JSON message bridge and a custom scheme. |
| Config | YAML via `YamlDotNet`, env overrides `YASS__section__key`, bound to records validated on start; a bad value stops startup and names the key. |
| Frontend | **SvelteKit** (static adapter, SPA fallback), TypeScript, **Tailwind**, `@vite-pwa/sveltekit`. No component library. |
| Shared types | Hub contract defined once in C# (`IYassHub`, `IYassClient`); TypeScript generated with `TypedSignalR.Client.TypeScript`; drift fails CI. |
| Desktop shell | **Photino.NET** 4.x: WebKitGTK 4.1 on Linux, WebView2 on Windows, WKWebView on macOS. |
| Tests | xUnit v3 + `WebApplicationFactory`; Vitest + Testing Library; a few Playwright smoke tests (Desktop Chrome) against the server in `--fake` mode. |
| Tooling | `pre-commit` (dotnet format, eslint, prettier, svelte-check, nixfmt, conventional-pre-commit), GitHub Actions, release-please, Nix flake (dev shell, packages, NixOS module `services.yass`), multi-stage `Dockerfile`. |
| Storage | Filesystem only, no database: a session is a directory with `session.json` and its page images. |

### Versions held back on purpose

- **NAPS2.Sdk 1.2.x.** 1.3's net10.0 assembly is marked `[RequiresPreviewFeatures]`, which spreads
  CA2252 errors through six projects. Move up when that marking is gone.
- **ImageSharp 3.1.x**, the major `NAPS2.Images.ImageSharp` uses. Transitive pinning is off, so a
  newer major would build and then break at runtime.
- **NAPS2.Wia**, referenced directly to ask WIA for device types, stays at the version NAPS2.Sdk pins.
- **TypeScript 6.x** until `svelte-check`, `@sveltejs/kit` and `typescript-eslint` accept 7.
- Test projects use **`xunit.v3.mtp-off`**: the default `xunit.v3` brings Microsoft Testing
  Platform, which would replace the VSTest coverage pipeline (`coverage.runsettings`,
  `scripts/coverage-gate.py`).

## 4. Architecture

```
src/Yass.Core      domain records, IScannerBackend, IDocumentWriter, hub contract
src/Yass.Scanning  NAPS2 adapter
src/Yass.Export    PDF and images, file name templates
src/Yass.App       host-agnostic app: config, sessions, scan loop, rendering, export, YassApi
src/Yass.Server    server host: SignalR hub, /img and /export endpoints, static web build, /healthz
src/Yass.Bridge    JSON message bridge and app scheme, shared by the in-process hosts
src/Yass.Desktop   desktop host: Photino window, native dialogs
src/Yass.Fake      simulated scanner and document writer (tests and --fake)
web/               SvelteKit app, one build for both hosts
nix/               packages and the NixOS module
```

- **`Yass.Core`** references nothing outside the BCL, apart from the hub abstractions and the
  attribute-only `TypedSignalR.Client.TypeScript.Attributes`. Enum members and DTOs need
  `[TranspilationSource]`, or the contract check fails.
- **`Yass.App`** uses no ASP.NET Core or SignalR types, only `Microsoft.Extensions.*`, so the
  desktop app does not need the ASP.NET Core runtime. `YassApi` implements the application side of the hub contract,
  `IClientNotifier` is the only way events go out, and `AddYassApp(...)` registers everything.
- **Application responsibilities:** `YassApi` adapts calls and translates service errors;
  `SessionService` owns session history operations and notifications, while `SessionStore` owns
  filesystem persistence. `ExportService` coordinates session state, render admission and progress;
  `ExportPlan` computes pages, names and metadata without I/O; `ExportDocumentWriter` writes and
  publishes each document and cleans up unfinished files. Keep these boundaries concrete; add an
  interface when there is a replaceable adapter, not merely for every class.
- **The hosts hold the transports and nothing else.** The server's hub delegates to `YassApi` and
  its HTTP endpoints only add ETag, cache headers and status codes. The desktop bridge dispatches
  by reflection over `IYassHub`. A new hub method therefore goes in `IYassHub`, `YassApi` and the
  hub's one-line delegation.
- **The frontend does not know its host.** `getHub()` picks SignalR or the bridge at runtime; both
  implement `ConnectionLike`.
- **Images over HTTP, state over the hub.** Thumbnails, previews and downloads are GET endpoints
  (`/img/{session}/{page}/{thumb|preview|frame}?h={hash}`, `/export/{session}/{file}`),
  cached as immutable by content hash; downloads are one-shot URLs. Everything else is hub
  calls and pushed events. The desktop serves the same paths from its scheme.
- **Scan loop:** iterate NAPS2's scan until the feeder is empty or `StopScan`; write each page to
  disk, push `PageAdded` at once, render the thumbnail in a bounded background queue, then push
  `PageUpdated`. Never block a hub or request thread on scanning or rendering.

## 5. Domain

- A session holds pages and the options used; its state runs Idle → Scanning → Review →
  Exporting → Done (or Failed).
- **Edits are non-destructive.** They are recorded in `session.json`; the scanned image is never
  changed. Derived images are a pure function of (source, edits), cached on disk by hash. One
  `RenderSpec` feeds both the hash and the pixels, so nothing reaches the image without reaching
  the hash. An edit added later joins the hash key only when set, so older pages keep their hashes.
- **Render order** (`PagePipeline`): crop (on the scan as it came) → quarter turns → skew (then
  cropped back to the page's proportions, so no corners show) → auto-levels → Enhance → brightness,
  contrast → despeckle → sharpen → grayscale / B&W threshold.

## 6. Scanning and export with NAPS2

- `CreateProcessedImage` and `BlankDetectionImageOp` are internal. Blank-page skipping and deskew
  are only available at scan time (`ScanOptions.ExcludeBlankPages`, `AutoDeskew`), so real scans
  never produce `IsBlank` pages; the fake backend does. `blank_page_threshold` (0..1) is scaled to
  NAPS2's 0..100.
- **Export** renders each page once with ImageSharp (edits, DPI) into a JPEG at
  `output.pdf.jpeg_quality` (PNG when lossless), imports it with `ImageImporter`, and assembles it
  with `PdfExporter`. The export's `ScanningContext` needs a `FileStorageManager` on its own temp
  folder: without one every page is decoded into memory (~26 MB per colour A4 page) and
  re-encoded. A `NAPS2.Util.ProgressHandler` carries the cancellation, so cancelling stops NAPS2;
  its reports fill the last 10 % of the progress bar.
- **Drivers:** `auto` is SANE on Linux, WIA on Windows and eSCL on macOS. NAPS2's Apple driver
  needs a `net8-macos` build that the NuGet package does not ship, so macOS gets eSCL only (USB
  scanners only if they speak it). TWAIN needs a 32-bit worker process and is left out.
- **Cameras are never listed.** SANE: drop ids from camera backends (`v4l`, `gphoto2`, `dc210`,
  `dc240`, `dc25`, `dmc`, `qcam`, `stv680`). WIA: ask each device's `DIP_DEV_TYPE` and drop cameras
  and video; if WIA cannot be asked, filter nothing rather than leave the list empty.
- If the NAPS2 API differs from what this file says, adapt the adapter, keep the domain model, and
  update this section.

## 7. Desktop app

- Photino wraps the platform webview: Linux needs `webkit2gtk-4.1`, `gtk3` and `libnotify`;
  Windows needs WebView2, which ships with Windows 10 and 11; macOS needs nothing extra. Photino's
  last release was January 2025; the bridge and scheme handler are small enough to move to another
  webview wrapper if it stalls.
- **The app is at `yass://app/`, except on Windows, where it is `https://yass.localhost/`.**
  Photino registers no custom scheme with WebView2, which then refuses to navigate to `yass://`
  and shows a black window. WebKitGTK and WKWebView do not let an app take over `https`. Either
  way the request reaches the handler, never the network.
- WebKitGTK and WKWebView have no `showSaveFilePicker`, so saving goes through the native dialog
  and a host-side file move; a document never passes through JavaScript.
- **The save dialog is opened directly** (`SaveDialog`: the Windows file dialog over COM, GTK 3's
  native chooser, `NSSavePanel`), not through Photino, whose dialog takes a starting folder only:
  it could not fill in the name, and on Windows did not select the format or add its extension.
- **No browser behaviour in the window**: F5/Ctrl+R, print, find, save page, zoom (keys and
  Ctrl+wheel), history (Alt+arrows, mouse back/forward), WebView2's history, downloads, favourites,
  new window and clear-data keys, middle-button autoscroll, drags out and file drops have their
  default stopped in the page (`$lib/browser-keys`). Only the default: the app's own shortcuts on
  those keys still run. Photino cannot turn off WebView2's accelerator keys natively, so this is the
  only guard. Middle and modified link clicks stay in the window, and only fields and
  `.select-text` (errors, scanner details) select like text.
- Config comes from `yass/yass.yaml` in the per-user config folder (`ApplicationData`) and may be
  absent; `server:` is ignored. Sessions live under `LocalApplicationData/yass/sessions` unless
  `sessions.directory` is set.
- **Packages** are all built on Linux by `scripts/package-desktop.sh`:
  - AppImage: appimagetool and its runtime are downloaded at pinned versions, checked by SHA-256,
    and run extract-and-run (runners have no FUSE). WebKitGTK is not bundled.
  - Windows: NSIS, per-user install into `%LOCALAPPDATA%\Programs\YASS` with no admin prompt.
    Install and uninstall clear that folder whole; scans and settings live elsewhere.
  - macOS: a zipped `YASS.app`, ad-hoc signed with rcodesign, since Apple Silicon will not start an
    unsigned arm64 binary. A zip because Linux cannot make a compressed `.dmg`.
  - Nothing is signed by a trusted identity, so SmartScreen and Gatekeeper warn on first launch.
- Icons: `scripts/render-icons.sh` renders `assets/logo/yass.ico` and `yass.icns` from
  `yass-512.png`; the outputs are committed.

## 8. UI

- **The UI comes first.** It can change every scan option; `yass.yaml` only supplies defaults
  (`scanner.defaults`), and nothing in the config hides a control. Scan settings, presets and
  scanner names live per browser in `localStorage`.
- The scanner is chosen on the home screen. Presets belong to the chosen scanner unless saved for
  all of them. Untouched settings start on the Document preset.
- Review: a page strip and a stage; the toolbar acts on the selected pages, or the page on the
  stage when none are selected. Blank pages are pre-marked deleted. Edits show optimistically and
  reconcile on `PageUpdated`.
- *Save to server* shows only when `output.directory` is set and this browser switched it on. It
  writes atomically and never overwrites (`-2`, `-3`… on a clash).
- The UI font is the platform's own (SF on Apple), falling back to Inter, self-hosted in
  `web/static/fonts/` because a home server may have no internet.
- **Big screens scale the whole UI** with CSS `zoom` on the root, in steps that keep at least
  1920×1000 to lay out in. Chromium and Firefox scale `vh`/`vw` by `zoom` and WebKit does not, so
  layout uses `--dvh`/`--vh`/`--vw` (`app.html` probes which), and pixels measured with
  `getBoundingClientRect()` are divided by `uiZoom()` before going into a style.
- **Targets:** first paint < 1 s over LAN; JS ≤ 150 kB gzipped; no image bytes held in JS beyond
  the current preview; every action keyboard-reachable; touch targets ≥ 44 px; works without the
  service worker.

## 9. Resource budget

Idle RSS ≤ 80 MB; a 50-page 300 dpi colour scan ≤ 400 MB peak; container image ≤ 200 MB. Desktop:
the same bounds for the .NET process (webview processes excluded), window up in < 1 s on a warm
start. Never hold more than `processing.max_concurrent_renders` decoded bitmaps in memory.

## 10. Testing

- 80 % line coverage gate on `Yass.*` (excluding `Yass.Fake`). There is little headroom:
  `Naps2ScannerBackend` has no tests because it needs a real scanner.
- `Yass.Server.Tests` cover `YassApi` through SignalR, `Yass.Desktop.Tests` through the bridge.
- Config tests round-trip every key and check that an invalid value names the key.
- Playwright runs a few smoke tests (scan to download, stopping a scan, saving to the server
  folder) on one desktop viewport against the real server in `--fake` mode. Only paths unit tests
  cannot reach go there: layout, animation and component behaviour are unit-tested, because
  browser timing made those e2e tests flaky and slow.
- **The fake scanner's pages are real documents**: Markdown in `src/Yass.Fake/Fixtures`, rendered
  at build time by `scripts/render-fixtures.sh` (pandoc → typst → poppler, A4 at 150 dpi) into
  `obj/`, never committed. A build needs pandoc, typst, poppler-utils and a font on
  `TYPST_FONT_PATHS` (DejaVu); all are in the dev shell, the Nix package and the Docker build
  stage. The script decides what is stale, because MSBuild's `Inputs` takes wildcards literally.
  The fake writer embeds the pages, so its PDFs can be looked at.

## 11. Commits

One logical change per commit, with a Conventional Commit message, each passing
`pre-commit run --all-files` and all tests. No monolithic commits.

Scopes: `core`, `app`, `fake`, `server`, `desktop`, `web`, `scanning`, `export`, `nix`, `docker`,
`ci`. Fixes discovered while working get their own `fix(scope):` commit, not squashed into a
feature.

`AnalysisLevel=latest-all` with `TreatWarningsAsErrors` turns each new SDK's analyzer wave into
build errors. Suppressions live in `.editorconfig` with their reasons; pin `AnalysisLevel` to
`10.0-all` if a wave is too big for one commit.

## 12. Working rules

1. Keep the app fully usable in `--fake` mode.
2. No new dependency without a line in this file saying why. Prefer the BCL and packages already
   in use.
3. Never block a hub or request thread on scanning or rendering.
4. The desktop app opens no listening socket.
5. Ask before: changing the config schema, adding a database, adding any auth.

## 13. Branding

**YASS** — Yet Another Slop Scanner. Logo: the "YASS!" cheer as an original flat-colour character
— a freshly scanned page with a grin, popping out of the scanner with both fists up, on a
sky-blue-to-deep-blue burst with sparkles; readable at 16 px, maskable at 512 px. SVG + PNG 192/512 +
favicon. No recognisable actor, film still or existing meme image.
