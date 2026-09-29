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
> Two consequences worth knowing:
>
> - The **export folder is set in the browser**. By default it is confined to
>   `output.directory` and its subfolders; `output.allowed_roots` widens that, and
>   `["/"]` removes the limit entirely. Whatever you allow, anyone who can reach
>   the port can write there, so run YASS as a user with no more filesystem reach
>   than it needs.
> - `naming.provider: anthropic` **sends the text of your documents to a third
>   party**. It is off by default; `ollama` keeps the text on your own machine.

## What it does

- **One tap to scan.** Big Scan button, live scanner status, pages appear as
  they come out of the feeder.
- **Review before saving.** Page strip beside the page on screen — tap a
  thumbnail to put it on the stage, tick its checkbox to include it in the
  selection the toolbar acts on — rotate, delete/restore, colour mode per page
  or for all, drag to reorder, full-page preview with swipe. Blank pages are
  detected and pre-marked deleted.
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
| `scanner.ui_editable` | `[source, dpi, color, page_size, blank_page_detection]` | Keys the Settings page may override (stored in the browser). |
| `output.directory` | `/data/consume` | Where PDFs are saved; point it at paperless' consume dir. |
| `output.filename_template` | `{date:yyyy-MM-dd}_{time:HHmm}_scan.pdf` | `{date:FMT}`, `{time:FMT}` (.NET formats), `{session}`. Existing names get `-2`, `-3`, … |
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

### Naming (optional)

YASS can suggest a file name from what is printed on the page. Everything here is
off by default — this is the only part of YASS that reads your documents.

| Key | Default | Meaning |
|---|---|---|
| `naming.ocr` | `false` | Read page text. Needs the `tesseract` binary on PATH (in the Docker image and the Nix package already). |
| `naming.ocr_language` | `eng` | Tesseract language code. |
| `naming.ocr_pages` | `2` | How many pages to read; a title is almost always on the first. |
| `naming.provider` | `off` | `off`, `ollama` or `anthropic`. |
| `naming.endpoint` | `""` | Base URL; defaults to `http://127.0.0.1:11434` for `ollama`. |
| `naming.model` | `llama3.2` | Model name to ask. |
| `naming.api_key_file` | `""` | Path to a file holding the API key. Never the key itself. |
| `naming.timeout_seconds` | `20` | Per-request timeout. |

With `ocr: true` and `provider: off` you get a suggestion from the largest heading
on the page plus one from your filename template, and nothing leaves the machine.
Adding `provider: ollama` asks a local model to summarise the page instead;
`anthropic` sends the page text to Anthropic's API. The suggestions popover says
which of those is happening.

### Setting up a model from the browser

Everything above configures the **server**. **Settings → Find name** configures
**this browser**, which needs no access to `yass.yaml` and no key file:

- **Anthropic** or **OpenAI-style** — paste an API key and the page calls the
  provider directly. The key is kept in this browser's `localStorage` and never
  reaches the scanner server. Verified to answer a web page:
  `api.anthropic.com`, `api.openai.com`, `openrouter.ai`, `api.groq.com`.
  Anything else that speaks the OpenAI chat shape works too — point the base URL
  at LM Studio, llama.cpp or Ollama. Ollama refuses requests from a page until
  `OLLAMA_ORIGINS` names your YASS origin.
- **In-browser (GPU)** — a model runs on your own machine through WebGPU, with
  no key and no third party. It downloads a few hundred megabytes the first
  time, then works offline. Needs WebGPU.
- **In-browser (CPU)** — the same idea without a graphics card: llama.cpp
  compiled to WebAssembly, decoding on the processor with the weights in
  ordinary RAM. Works in any browser. Pick one of the models offered (145 MB to
  800 MB) or paste the URL of any instruct-tuned GGUF. Budget roughly the
  download size again in free RAM while it answers, and expect a few seconds
  rather than a fraction of one — it runs on a single core, because more than
  that needs the page to be cross-origin isolated.

Both in-browser options need `naming.ocr: true` on the server, because a model
in the browser reads text and cannot see the page. Where the server reads
nothing, the suggestions popover says so instead of coming back empty.

Two things worth knowing. A key in `localStorage` can be read by anything running
on that page and stays until you clear it, so think twice on a browser other
people use. And where the server does not read page text, the **page image** is
sent to the model instead of a few lines of text — the settings sheet says so,
and the toggle beside it turns that off.

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
