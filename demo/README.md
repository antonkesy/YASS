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
4. **Name it** — the scan is already called what the file name template says. Put
   `{NAME}` in that template and the slot shows up inside the document name as
   **name it**; click it and the page is read with `tesseract` (in the image, and
   on by default) and the slot filled. Clicking again offers another go, the other
   candidates, or the whole name to type yourself. `{YEAR}-{MONTH}-{DAY}_{NAME}`
   keeps the date; the **Ready-made** button offers whole templates, and **Found
   names** picks `lower_snake` or `lower-kebab`. For a name written rather than
   copied, point **Settings → Find name** at a model: **In-browser (CPU)** needs
   no key and no account, and downloads its weights once.
5. **Save** — the PDFs land in [`output/`](output) on your host, the demo's
   stand-in for a paperless-ngx consume folder.
   **Download** hands them to the browser instead.
6. **Past scans** — every scan is listed with its status, searchable and
   sortable, with a select mode for bulk download and delete.
7. **Settings** — presets, source, dpi, colour, page size, blank-page detection,
   file format and the name template. All stored in the browser, not on the server.

`Ctrl-C` stops it; `docker compose down` removes the container. Scan sessions
live in the container's `/tmp`, so nothing survives except the PDFs you saved.

## What is simulated

`--fake` replaces the scanner and the PDF writer. Both produce real output:

- The pages are an invoice, a letter, a payslip, a report in colour and a
  near-blank sheet, written as Markdown in
  [`src/Yass.Fake/Fixtures`](../src/Yass.Fake/Fixtures) and rendered to A4 at
  150 dpi by the build. They are documents with titles on them, which is what
  makes naming worth watching here. They cycle, so `pages=9` shows each
  more than once; to change what a page says, edit its Markdown and rebuild.
- The saved PDF holds those pages as images, rotated and greyed the way your
  edits ask. It opens in any reader and shows the document. A scan you have
  named uses that name; an unnamed one falls back to `fake_<timestamp>.pdf`
  rather than `output.filename_template`, which only the real store expands.

What is *not* simulated is the scanner itself: there is no glass, no feeder and
no driver. For that you need hardware and the non-fake path — see the
[main README](../README.md).

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
