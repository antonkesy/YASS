# YASS — Yet Another Scanner Service

<img src="assets/logo/yass.svg" alt="" width="96" align="right">

Headless scanning service for a home server. Attach a scanner (USB/SANE or
network/eSCL), open the mobile-first web UI, scan until the feeder is empty,
review the pages, and save a PDF straight into your
[paperless-ngx](https://docs.paperless-ngx.com/) consume folder.

> ## ⚠️ No authentication — LAN only
>
> YASS has **no login, no tokens, no TLS**. Anyone who can reach the port can
> scan, view and delete documents. It is designed for a trusted home network and
> must **never be exposed to the internet**: bind it to a LAN address, keep it
> behind your firewall, and do not put it behind a public reverse proxy.
>
> One consequence worth knowing: the **export folder is set in the browser**. By
> default it is confined to `output.directory` and its subfolders;
> `output.allowed_roots` widens that, and `["/"]` removes the limit entirely.
> Whatever you allow, anyone who can reach the port can write there, so run YASS
> as a user with no more filesystem reach than it needs.

## Screenshots

<p align="center">
  <img src="assets/screenshots/home.png" alt="Home screen: scanner picker, presets and the Scan button" width="24%">
  <img src="assets/screenshots/review.png" alt="Review screen: the page on the stage, the toolbar under it, the page strip below" width="24%">
  <img src="assets/screenshots/all-pages.png" alt="All pages view: the whole scan on one screen with a blank page pre-marked deleted" width="24%">
  <img src="assets/screenshots/past-scans.png" alt="Past scans list on the home screen" width="24%">
</p>

<p align="center">
  <img src="assets/screenshots/review-desktop.png" alt="Review screen on a desktop browser: page strip, stage, and the export panel" width="98%">
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
- **Export** writes the PDF atomically into `output.directory` under a templated
  name — point that at your paperless-ngx consume folder, or anywhere else.
  **Save as…** puts a copy on the machine you are browsing from: it opens the
  browser's own save dialog over https or on localhost, and is a plain download
  anywhere else (the button then says *Download*, and your browser's "ask where
  to save each file" setting is what brings the dialog back).
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
      - /srv/paperless/consume:/data/consume
    devices:
      - /dev/bus/usb:/dev/bus/usb       # USB/SANE scanners
volumes:
  yass-sessions:
```

See [`docker-compose.example.yml`](docker-compose.example.yml) for the
commented version (eSCL scanners want `network_mode: host` for mDNS discovery).
Copy [`yass.example.yaml`](yass.example.yaml) to `yass.yaml`, set
`output.directory` to `/data/consume`, and start:

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
      output.directory = "/var/lib/paperless/consume";
    };
  };
}
```

`nix run github:OWNER/yass -- --fake` starts the simulated scanner locally.

### From source

```sh
nix develop                      # dotnet 10, node 24, pnpm, sane-backends, pre-commit
pnpm --dir web install && pnpm --dir web build
YASS_WEBROOT=$PWD/web/build dotnet run --project src/Yass.Server -- --fake
```

## Configuration

Everything lives in one YAML file (`--config PATH`, or `YASS_CONFIG`; missing
file = defaults). Any key can be overridden with an environment variable:
`YASS__section__key`, e.g. `YASS__output__directory=/mnt/consume`. Invalid
values stop startup and name the offending key.

| Key | Default | Meaning |
|---|---|---|
| `server.listen` | `0.0.0.0:8080` | Bind address. **No auth: use a LAN address.** Not reloadable. |
| `server.base_path` | `/` | Path prefix when served under a sub-path. |
| `scanner.driver` | `auto` | `auto`, `sane` or `escl`. |
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
| `output.directory` | `/data/consume` | Where PDFs are saved; point it at paperless' consume dir. |
| `output.filename_template` | `{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf` | `{date:FMT}`, `{time:FMT}` (.NET formats), `{session}`. Existing names get `-2`, `-3`, …. `{NAME}` is **not** a key here — it is a browser token, below. |
| `output.pdf.compression` | `jpeg` | `jpeg` or `lossless`. |
| `output.pdf.jpeg_quality` | `75` | 1–100. |
| `output.allowed_roots` | `[]` | Folders the UI may export into, each including everything beneath it. Empty = `output.directory` only. `["/"]` = anywhere. |
| `output.allow_download` | `true` | Show the Save as… button, which copies the document to the browsing machine. |
| `output.on_success` | `keep_session` | `keep_session` or `delete_session` after a successful save. |
| `processing.max_concurrent_renders` | `2` | Upper bound on decoded bitmaps in memory. |
| `processing.thumbnail_width` | `320` | px |
| `processing.preview_max_width` | `1600` | px |
| `sessions.directory` | `/data/sessions` | One directory per scan: `session.json` + `page-NNN.png` + caches. |
| `sessions.retention_hours` | `24` | Sessions older than this are deleted hourly; `0` disables. |
| `logging.level` | `info` | `trace` … `critical`. |
| `logging.format` | `text` | `text` or `json`. |

Files are written atomically: the PDF is streamed to a hidden `._yass-*.tmp`
file in the consume directory (matched by paperless' default ignore pattern) and
renamed into place once complete, so the consumer never sees a partial document.

### Naming a scan

A finished scan is already called what your file name template says. The list of
past scans calls each scan the same thing the review screen does, and the search
box and the A–Z sort go by that name too — so a scan nobody has named is listed
under the file name it would be saved as, not under the time of day it happened.
Open it and rename it whenever you like; the list follows.

## Scanner setup

- **USB (SANE):** the host needs `sane-backends` and the device must be
  readable by the service user (`scanner`/`lp` group, or udev rules). Check with
  `scanimage -L`. In Docker, pass `/dev/bus/usb`.
- **Network (eSCL / AirScan):** set `scanner.driver: escl`. Discovery uses mDNS,
  so use `network_mode: host` in Docker. Many modern scanners support this
  without any driver.
- `scanner.device` accepts a substring, e.g. `epsonds` or `DS-530`.

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
dotnet test                          # xUnit: core, scanning, export, server (WebApplicationFactory)
pnpm --dir web test                  # vitest
pnpm --dir web test:e2e              # Playwright, desktop + Pixel 7, against `--fake`
pnpm --dir web hub:generate          # regenerate TS hub client from the C# contract
```

The commit plan, stack decisions and domain model are in [SPEC.md](SPEC.md);
deviations are recorded in [`docs/decisions/`](docs/decisions/). Commits follow
Conventional Commits; releases are cut by release-please.

Layout: `src/Yass.Core` (domain, config, hub contract), `Yass.Scanning`
(NAPS2 adapter), `Yass.Export` (PDF, templating, atomic write),
`Yass.Server` (host, hub, image endpoints), `Yass.Fake` (simulated scanner),
`web/` (SvelteKit PWA), `nix/` (package, NixOS module).

## License

MIT — see [LICENSE](LICENSE).
