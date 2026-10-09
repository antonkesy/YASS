# YASS — Yet Another Slop Scanner

<img src="assets/logo/yass.svg" alt="" width="96" align="right">

Headless scanning service for a home server. Attach a scanner (USB/SANE or
network/eSCL), open the mobile-first web UI, scan until the feeder is empty,
review the pages, and download the PDF to whatever device you are holding.
The same app also runs as a [desktop program](#desktop-app) with no network
port at all.

> ## ⚠️ No authentication — LAN only
>
> YASS has **no login, no tokens, no TLS**. Anyone who can reach the port can
> scan, view and delete documents. It is designed for a trusted home network and
> must **never be exposed to the internet**: bind it to a LAN address, keep it
> behind your firewall, and do not put it behind a public reverse proxy.
>
> The server writes documents nowhere but its own session directory: a finished
> scan leaves the machine as a browser download, never into a folder someone
> typed into the UI.

## Screenshots

<p align="center">
  <img src="assets/screenshots/home.png" alt="Home screen: scanner picker, presets and the Scan button" width="24%">
  <img src="assets/screenshots/review.png" alt="Review screen: the page on the stage, the toolbar under it, the page strip below" width="24%">
  <img src="assets/screenshots/all-pages.png" alt="All pages view: the whole scan on one screen with a blank page pre-marked deleted" width="24%">
  <img src="assets/screenshots/past-scans.png" alt="Past scans list on the home screen" width="24%">
</p>

<p align="center">
  <img src="assets/screenshots/review-desktop.png" alt="Review screen on a desktop browser: page strip, stage, and the save panel" width="98%">
</p>

Taken against the simulated scanner (`--fake`); regenerate them with
`pnpm --dir web screenshots`.

## What it does

- **One tap to scan.** Big Scan button, live scanner status, pages appear as
  they come out of the feeder.
- **Review before saving.** Page strip beside the page on screen — tap a
  thumbnail to put it on the stage and the toolbar under it edits that page:
  rotate, delete/restore, colour mode, crop, full-page preview with swipe, drag
  to reorder. **All pages** opens the scan sized to fit the screen, and that is
  where you tick several pages to edit them at once. Blank pages are detected and
  pre-marked deleted.
- **Save as…** puts the document on the machine you are browsing from, as a
  PDF or as one image per page: it opens the browser's own save dialog over
  https or on localhost, and is a plain download anywhere else (the button then
  says *Download*, and your browser's "ask where to save each file" setting is
  what brings the dialog back).
- **Save to server**, optionally: set `output.directory` (a paperless-ngx consume
  folder, say) and each browser can switch on a *Save to server* button in its
  Settings. Files are written atomically and never overwrite one another; the
  folder is fixed in the config, so a browser cannot write anywhere else.
- **Installable PWA**, works without a service worker, everything reachable by
  keyboard, touch targets ≥ 44 px.
- **Single config file** (`yass.yaml`), every key overridable through the
  environment; non-`server` keys reload at runtime.
- **Fake mode** (`--fake`) simulates a scanner so you can try the UI, run the
  test-suite, or develop without hardware.

Backend: .NET 10 with [NAPS2.Sdk](https://www.naps2.com/sdk) (SANE + eSCL),
SignalR over MessagePack. Frontend: SvelteKit + Tailwind. Storage: plain
directories, no database.

## Quickstart

### Just want to see it?

```sh
cd demo && docker compose up --build   # then open http://localhost:8080
```

[`demo/`](demo) is a self-contained, throwaway setup: fake scanner, no hardware,
saved PDFs land in `demo/output/`. See [`demo/README.md`](demo/README.md).

### Docker Compose (recommended)

```yaml
services:
  yass:
    build: .                            # clone this repo; no prebuilt image is published
    restart: unless-stopped
    ports:
      - "192.168.1.10:8080:8080"        # LAN address of the host
    volumes:
      - ./yass.yaml:/etc/yass/yass.yaml:ro
      - yass-sessions:/data/sessions
    devices:
      - /dev/bus/usb:/dev/bus/usb       # USB/SANE scanners
volumes:
  yass-sessions:
```

See [`docker-compose.example.yml`](docker-compose.example.yml) for the
commented version (eSCL scanners want `network_mode: host` for mDNS discovery).
Copy [`yass.example.yaml`](yass.example.yaml) to `yass.yaml`, adjust it, and
start:

```sh
docker compose up -d
open http://192.168.1.10:8080
```

Try it without a scanner:

```sh
docker build -t yass .
docker run --rm -p 8080:8080 yass --fake
```

### NixOS

```nix
{
  inputs.yass.url = "github:OWNER/yass";

  # in your configuration:
  imports = [ inputs.yass.nixosModules.default ];
  hardware.sane.enable = true;
  services.yass = {
    enable = true;
    settings = {
      server.listen = "192.168.1.10:8080";
      scanner.device = "epsonds";
    };
  };
}
```

`nix run github:OWNER/yass -- --fake` starts the simulated scanner locally.

### Desktop app

The same app also runs as a desktop program on Linux, Windows and macOS, for a
scanner plugged into the computer you are sitting at. It shows the same screens
in a native window ([Photino](https://www.tryphotino.io/): WebKitGTK, WebView2
or WKWebView) and **opens no network port at all, not even on localhost**.
The window talks to the scanning code inside the same process, and a saved
document is moved on disk to the file you pick, never passed through the page.
The only network traffic is to an eSCL scanner, if you use one.

- **Download:** every [GitHub release](https://github.com/antonkesy/YASS/releases)
  carries self-contained zips for `linux-x64`, `win-x64` and `osx-arm64`, with a
  `SHA256SUMS`. Unzip and run `Yass.Desktop`
  (`Yass.Desktop.exe` on Windows). It needs:
  - **Linux:** WebKitGTK 4.1 (`webkit2gtk-4.1` / `libwebkit2gtk-4.1-0`).
  - **Windows:** the WebView2 runtime, which ships with Windows 10 and 11.
  - **macOS:** the build is unsigned, so clear the quarantine flag once with
    `xattr -dr com.apple.quarantine yass-desktop-*`.
- **Nix:** `nix run github:OWNER/yass#desktop -- --fake`. Off NixOS, a
  Nix-built GUI app needs your GPU driver; `nix develop` sets that up for
  development.
- **From source:** `pnpm --dir web build`, then
  `YASS_WEBROOT=$PWD/web/build dotnet run --project src/Yass.Desktop -- --fake`
  inside `nix develop`.

**Config.** The same `yass.yaml`, read from your per-user config folder:
`~/.config/yass/` on Linux, `%APPDATA%\yass\` on Windows, or
`~/Library/Application Support/yass/` on macOS. The file is optional, and
`--config PATH` points elsewhere. `server:` is ignored. Sessions go to the
per-user data folder unless `sessions.directory` is set, and renders use up to
four cores unless `processing.max_concurrent_renders` says otherwise.

**Saving.** *Save as…* opens the system's save dialog, and *Download all*
asks for a folder. *Save to folder* is *Save to server* under another name:
it saves into `output.directory`.

**Scanners.** `scanner.driver: auto` is SANE on Linux, WIA on Windows and eSCL
on macOS. NAPS2's Apple driver needs a macOS-only build of the SDK, so USB
scanners on a Mac only work if they speak eSCL ([ADR-004](docs/decisions/ADR-004-naps2-api-adaptation.md)).

### From source

```sh
nix develop                      # dotnet 10, node 24, pnpm, sane-backends, pre-commit
pnpm --dir web install && pnpm --dir web build
YASS_WEBROOT=$PWD/web/build dotnet run --project src/Yass.Server -- --fake
```

## Configuration

Everything lives in one YAML file (`--config PATH`, or `YASS_CONFIG`; missing
file = defaults). Any key can be overridden with an environment variable:
`YASS__section__key`, e.g. `YASS__sessions__directory=/mnt/yass`. Invalid
values stop startup and name the offending key.

| Key | Default | Meaning |
|---|---|---|
| `server.listen` | `0.0.0.0:8080` | Bind address. **No auth: use a LAN address.** Not reloadable. |
| `server.base_path` | `/` | Path prefix when served under a sub-path. |
| `scanner.driver` | `auto` | `auto`, `sane`, `escl` or `wia`. `auto` is SANE on Linux, WIA on Windows and eSCL on macOS. |
| `scanner.device` | `""` | NAPS2 device id or a substring of id/name; empty = first found. |
| `scanner.poll_interval_seconds` | `5` | Scanner readiness poll. |
| `scanner.defaults.source` | `feeder` | `flatbed`, `feeder` or `duplex`. |
| `scanner.defaults.dpi` | `300` | 50–1200. |
| `scanner.defaults.color` | `grayscale` | `bw`, `grayscale` or `color`. |
| `scanner.defaults.page_size` | `a4` | `a4`, `a5`, `letter`, `legal`. |
| `scanner.defaults.brightness` / `contrast` | `0` | −100..100. |
| `scanner.defaults.blank_page_detection` | `true` | Blank pages are skipped (real scanner) or pre-marked deleted (fake). |
| `scanner.defaults.blank_page_threshold` | `0.02` | 0..1, scaled onto NAPS2's coverage threshold. |
| `scanner.defaults.auto_deskew` | `false` | Straighten pages during the scan. |
| `scanner.defaults.device` | `""` | Default scanner, by exact device id; empty = whatever `scanner.device` resolves to. The picker on the home screen overrides it per browser. |
| `scanner.ui_editable` | `[]` | Deprecated and ignored: every scan setting can be changed from the UI (stored in the browser), and `scanner.defaults` only supplies the defaults. Still accepted so older files load. |
| `output.directory` | `""` | Absolute folder on the server documents can be saved into from the review screen; empty turns it off and hides it from the UI. |
| `output.pdf.compression` | `jpeg` | `jpeg` or `lossless`. |
| `output.pdf.jpeg_quality` | `75` | 1–100. |
| `processing.max_concurrent_renders` | `2` | Upper bound on decoded bitmaps in memory. |
| `processing.thumbnail_width` | `320` | px |
| `processing.preview_max_width` | `1600` | px |
| `sessions.directory` | `/data/sessions` | One directory per scan: `session.json` + `page-NNN.png` + caches. |
| `sessions.retention_hours` | `24` | Sessions older than this are deleted hourly; `0` disables. |
| `logging.level` | `info` | `trace` … `critical`. |
| `logging.format` | `text` | `text` or `json`. |

### Naming a scan

A finished scan is already called what your file name template says. The list of
past scans calls each scan the same thing the review screen does, and the search
box and the A–Z sort go by that name too — so a scan nobody has named is listed
under the file name it would be downloaded as, not under the time of day it happened.
Open it and rename it whenever you like; the list follows.

## Scanner setup

- **USB (SANE):** the host needs `sane-backends` and the device must be
  readable by the service user (`scanner`/`lp` group, or udev rules). Check with
  `scanimage -L`. In Docker, pass `/dev/bus/usb`.
- **Network (eSCL / AirScan):** set `scanner.driver: escl`. Discovery uses mDNS,
  so use `network_mode: host` in Docker. Many modern scanners support this
  without any driver.
- `scanner.device` accepts a substring, e.g. `epsonds` or `DS-530`.
- **Cameras are never scan sources.** Webcams and digital cameras that SANE or WIA
  lists (`v4l`, `gphoto2`, WIA cameras and the like) are left out of the scanner list, so a
  laptop's built-in camera does not show up as a scanner.

## HTTP surface

| Endpoint | Purpose |
|---|---|
| `GET /` | The web UI (SvelteKit SPA). |
| `/hub` | SignalR hub (WebSocket, MessagePack): state, commands, events. |
| `GET /img/{session}/{page}/thumb\|preview?h=HASH` | WebP images, immutable-cached by content hash. |
| `GET /export/{session}/{token}.EXT` | One-shot download URL returned by *Save as…*; `.pdf`, `.jpg` or `.png`. |
| `GET /healthz` | Liveness. |

## Development

```sh
nix develop
pre-commit install
dotnet test                          # xUnit: core, scanning, export, server (WebApplicationFactory), desktop (bridge)
pnpm --dir web test                  # vitest
pnpm --dir web test:e2e              # Playwright, desktop + Pixel 7, against `--fake`
pnpm --dir web hub:generate          # regenerate TS hub client from the C# contract
```

The commit plan, stack decisions and domain model are in [SPEC.md](SPEC.md);
deviations are recorded in [`docs/decisions/`](docs/decisions/). Commits follow
Conventional Commits; releases are cut by release-please: it keeps a release PR
open on `main` with the next version and changelog, and merging it tags `vX.Y.Z`,
publishes the GitHub release and attaches the desktop zips
([`release.yml`](.github/workflows/release.yml)).

Layout: `src/Yass.Core` (domain, config, hub contract), `Yass.Scanning`
(NAPS2 adapter), `Yass.Export` (PDF and image writing, name templating),
`Yass.App` (everything both hosts share: sessions, scan loop, rendering,
export, `YassApi`), `Yass.Server` (web host: SignalR hub, image endpoints),
`Yass.Desktop` (desktop host: Photino window, `yass://` scheme, message
bridge), `Yass.Fake` (simulated scanner), `web/` (SvelteKit PWA, one build for
both hosts), `nix/` (packages, NixOS module). `scripts/publish-desktop.sh`
builds the desktop zips.

## License

MIT — see [LICENSE](LICENSE).
