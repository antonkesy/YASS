# YASS demo

A complete, throwaway YASS in one command — no scanner, no paperless-ngx, no
config to fill in.

```sh
cd demo
docker compose up --build
```

Then open <http://localhost:8080> (first build takes a few minutes; after that
it starts in seconds). If something else already has 8080, pick another port:

```sh
YASS_PORT=9000 docker compose up --build
```

## What to try

1. **Scan** — the scripted fake scanner feeds five pages, ~400 ms each. Page 2
   comes out blank and starts out deleted.
2. **Review** — rotate, crop, filter, reorder by dragging, delete and restore.
   Open a page and click it (or the zoom button, or `z`) to switch between
   fit-to-screen and full width; `Esc` backs out of zoom, then closes.
3. **Split** — select a page and press **Split**: it opens a second document,
   badged `Doc 2` in the filmstrip, and **Save** then writes one PDF per document.
4. **Name it** — type in the document name field, or press **Find name**, which
   reads the page with `tesseract` (in the image, and on by default) and offers
   what it found. For a name written rather than copied, point **Settings → Find
   name** at a model: **In-browser (CPU)** needs no key and no account, and
   downloads its weights once.
5. **Save** — the PDFs land in [`output/`](output) on your host, the demo's
   stand-in for a paperless-ngx consume folder.
   **Download** hands them to the browser instead.
6. **Past scans** — every scan is listed with its status, searchable and
   sortable, with a select mode for bulk download and delete.
7. **Settings** — presets, source, dpi, colour, page size, blank-page detection,
   file format and the name template. All stored in the browser, not on the server.

`Ctrl-C` stops it; `docker compose down` removes the container. Scan sessions
live in the container's `/tmp`, so nothing survives except the PDFs you saved.

## Two things are simulated

`--fake` replaces both the scanner and the PDF writer, so:

- The pages are **PNG fixtures embedded in `Yass.Fake`** (three text pages, one
  colour page, one near-blank), not files you can swap out. They cycle, so
  `pages=9` shows each fixture more than once.
- The saved PDF is a **text-only placeholder** — one line per page recording the
  source file, rotation and colour mode. It is a valid PDF and proves the save
  path works, but it does not contain the scanned images. A scan you have named
  uses that name; an unnamed one falls back to `fake_<timestamp>.pdf` rather than
  `output.filename_template`, which only the real store expands.

For real pages and a real PDF you need a scanner and the non-fake path — see the
[main README](../README.md).

The fixtures are also only 620 px wide, so zooming in looks soft here. Real
scans are capped by `processing.preview_max_width` (1600 by default, up to
4096) — raise it if zoom looks blurry on a large screen.

## Changing the scenario

The `command:` in [`docker-compose.yml`](docker-compose.yml) scripts the fake
scanner. Keys, comma-separated:

| Key | Meaning | Example |
| --- | --- | --- |
| `pages` | pages in the feeder | `pages=12` |
| `blank` | 1-based pages that come out blank, `;`-separated | `blank=3;7` |
| `error` | jam after this many pages | `error=4` |
| `delay` | time per page | `delay=2s` |
| `nodevice` | report no scanner at all | `nodevice` |
| `device` | name shown in the UI | `device=Epson DS-530II` |

So `command: ["--fake", "pages=20,blank=2;5;9,error=12,delay=1s"]` gives a long,
slow scan that jams part-way — useful for exercising the error and progress UI.

Everything else lives in [`yass.yaml`](yass.yaml); non-`server` keys reload at
runtime, so you can edit it and hit reload without restarting the container.

The export folder you can type in Settings is confined to `output.allowed_roots`,
which the demo leaves empty — so it is `/data/consume` and below, i.e. `output/`
on your host. That is the default everywhere, not a demo-only restriction.

## If the PDFs are not writable

The container runs as uid 1000 so `output/` stays yours. If your uid differs:

```sh
UID=$(id -u) GID=$(id -g) docker compose up --build
```

## If port 8080 is taken

`YASS_PORT` moves the host side of the mapping. The container always listens on
8080, so only the number you type in the browser changes:

```sh
YASS_PORT=9000 docker compose up --build   # then http://localhost:9000
```

To stop repeating it, put it in `demo/.env`, which compose reads on its own:

```sh
echo YASS_PORT=9000 > .env
```

The address stays `127.0.0.1`, whatever the port: YASS has no authentication, so
the demo does not listen beyond this machine. Exposing it to a LAN is a decision
for [`docker-compose.example.yml`](../docker-compose.example.yml), not the demo.
