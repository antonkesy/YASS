# YASS demo

A complete, throwaway YASS in one command — no scanner, no paperless-ngx, no
config to fill in.

```sh
cd demo
docker compose up --build
```

Then open <http://localhost:8080> (first build takes a few minutes; after that
it starts in seconds).

## What to try

1. **Scan** — the scripted fake scanner feeds five pages, ~400 ms each. Page 3
   comes out blank and is flagged as such.
2. **Review** — rotate, reorder by dragging, delete the blank page.
   Open a page and click it (or the zoom button, or `z`) to switch between
   fit-to-screen and full width; `Esc` backs out of zoom, then closes.
3. **Save** — the PDF lands in [`output/`](output) on your host, the demo's
   stand-in for a paperless-ngx consume folder.
   **Download** hands it to the browser instead.
4. **Settings** — source, dpi, colour, page size and blank-page detection are
   editable from the UI and stored in the browser.

`Ctrl-C` stops it; `docker compose down` removes the container. Scan sessions
live in the container's `/tmp`, so nothing survives except the PDFs you saved.

## Two things are simulated

`--fake` replaces both the scanner and the PDF writer, so:

- The pages are **PNG fixtures embedded in `Yass.Fake`** (three text pages, one
  colour page, one near-blank), not files you can swap out. They cycle, so
  `pages=9` shows each fixture more than once.
- The saved PDF is a **text-only placeholder** — one line per page recording the
  source file, rotation and colour mode. It is a valid PDF and proves the save
  path works, but it does not contain the scanned images. It is also named
  `fake_<timestamp>.pdf`, ignoring `output.filename_template`.

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

## If the PDFs are not writable

The container runs as uid 1000 so `output/` stays yours. If your uid differs:

```sh
UID=$(id -u) GID=$(id -g) docker compose up --build
```
