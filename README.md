# YAWSS — Yet Another Web Scanner Service

<img src="assets/logo/yawss.svg" alt="" width="96" align="right">

Headless scanning service for a home server. Attach a scanner (USB/SANE or
network/eSCL), open the mobile-first web UI, scan until the feeder is empty,
review the pages, and save a PDF straight into your
[paperless-ngx](https://docs.paperless-ngx.com/) consume folder.

> ## ⚠️ No authentication — LAN only
>
> YAWSS has **no login, no tokens, no TLS**. Anyone who can reach the port can
> scan, view and delete documents. It is designed for a trusted home network and
> must **never be exposed to the internet**: bind it to a LAN address, keep it
> behind your firewall, and do not put it behind a public reverse proxy.

## What it does

- **One tap to scan.** Big Scan button, live scanner status, pages appear as
  they come out of the feeder.
- **Review before saving.** Thumbnail grid (2 columns on a phone, 6 on a
  desktop), multi-select, rotate, delete/restore, colour mode per page or for
  all, drag to reorder, full-page preview with swipe. Blank pages are detected
  and pre-marked deleted.
- **Save** writes the PDF atomically into `output.directory` under a templated
  name — point that at your paperless-ngx consume folder, or anywhere else;
  **Download** hands the PDF to the browser instead.
- **Installable PWA**, works without a service worker, everything reachable by
  keyboard, touch targets ≥ 44 px.
- **Single config file** (`yawss.yaml`), every key overridable through the
  environment; non-`server` keys reload at runtime.
- **Fake mode** (`--fake`) simulates a scanner so you can try the UI, run the
  test-suite, or develop without hardware.

Backend: .NET 8 with [NAPS2.Sdk](https://www.naps2.com/sdk) (SANE + eSCL),
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
  yawss:
    build: .                            # clone this repo; no prebuilt image is published
    restart: unless-stopped
    ports:
      - "192.168.1.10:8080:8080"        # LAN address of the host
    volumes:
      - ./yawss.yaml:/etc/yawss/yawss.yaml:ro
      - yawss-sessions:/data/sessions
      - /srv/paperless/consume:/data/consume
    devices:
      - /dev/bus/usb:/dev/bus/usb       # USB/SANE scanners
volumes:
  yawss-sessions:
```

See [`docker-compose.example.yml`](docker-compose.example.yml) for the
commented version (eSCL scanners want `network_mode: host` for mDNS discovery).
Copy [`yawss.example.yaml`](yawss.example.yaml) to `yawss.yaml`, set
`output.directory` to `/data/consume`, and start:

```sh
docker compose up -d
open http://192.168.1.10:8080
```

Try it without a scanner:

```sh
docker build -t yawss .
docker run --rm -p 8080:8080 yawss --fake
```

### NixOS

```nix
{
  inputs.yawss.url = "github:OWNER/yawss";

  # in your configuration:
  imports = [ inputs.yawss.nixosModules.default ];
  hardware.sane.enable = true;
  services.yawss = {
    enable = true;
    settings = {
      server.listen = "192.168.1.10:8080";
      scanner.device = "epsonds";
      output.directory = "/var/lib/paperless/consume";
    };
  };
}
```

`nix run github:OWNER/yawss -- --fake` starts the simulated scanner locally.

### From source

```sh
nix develop                      # dotnet 8, node 22, pnpm, sane-backends, pre-commit
pnpm --dir web install && pnpm --dir web build
YAWSS_WEBROOT=$PWD/web/build dotnet run --project src/Yawss.Server -- --fake
```

## Configuration

Everything lives in one YAML file (`--config PATH`, or `YAWSS_CONFIG`; missing
file = defaults). Any key can be overridden with an environment variable:
`YAWSS__section__key`, e.g. `YAWSS__output__directory=/mnt/consume`. Invalid
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
| `scanner.ui_editable` | `[source, dpi, color, page_size, blank_page_detection]` | Keys the Settings page may override (stored in the browser). |
| `output.directory` | `/data/consume` | Where PDFs are saved; point it at paperless' consume dir. |
| `output.filename_template` | `{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf` | `{date:FMT}`, `{time:FMT}` (.NET formats), `{session}`. Existing names get `-2`, `-3`, … |
| `output.pdf.compression` | `jpeg` | `jpeg` or `lossless`. |
| `output.pdf.jpeg_quality` | `75` | 1–100. |
| `output.allow_download` | `true` | Show the Download button. |
| `output.on_success` | `keep_session` | `keep_session` or `delete_session` after a successful save. |
| `processing.max_concurrent_renders` | `2` | Upper bound on decoded bitmaps in memory. |
| `processing.thumbnail_width` | `320` | px |
| `processing.preview_max_width` | `1600` | px |
| `sessions.directory` | `/data/sessions` | One directory per scan: `session.json` + `page-NNN.png` + caches. |
| `sessions.retention_hours` | `24` | Sessions older than this are deleted hourly; `0` disables. |
| `logging.level` | `info` | `trace` … `critical`. |
| `logging.format` | `text` | `text` or `json`. |

Files are written atomically: the PDF is streamed to a hidden `._yawss-*.tmp`
file in the consume directory (matched by paperless' default ignore pattern) and
renamed into place once complete, so the consumer never sees a partial document.

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
| `GET /export/{session}/{token}.pdf` | One-shot download URL returned by *Download*. |
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

Layout: `src/Yawss.Core` (domain, config, hub contract), `Yawss.Scanning`
(NAPS2 adapter), `Yawss.Export` (PDF, templating, atomic write),
`Yawss.Server` (host, hub, image endpoints), `Yawss.Fake` (simulated scanner),
`web/` (SvelteKit PWA), `nix/` (package, NixOS module).

## License

MIT — see [LICENSE](LICENSE).
