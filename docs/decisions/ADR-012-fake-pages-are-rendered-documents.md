# ADR-012: The fake scanner's pages are rendered documents, and its PDF is a real one

- **Status:** accepted
- **Date:** 2026-09-29

## Context

`--fake` stands in for the scanner and the PDF writer so the whole flow can be exercised with
no hardware. Two things it produced were placeholders rather than stand-ins:

- The pages were five hand-drawn PNGs: a big digit, one line of text, and grey bars where the
  body should be. Nothing about them looked like paper anyone would scan, so the review
  screen the demo's own README tells you to try had nothing worth reviewing.
- The saved PDF held one line of text per page naming the source file and its rotation. It was
  a valid PDF and proved the save path ran, but it did not contain the scan, so nothing about
  it could be looked at.

A placeholder that cannot be read and cannot be looked at proves less than it appears to.

## Decision

The pages are **documents, written as Markdown and rendered by the build**: an invoice, a
letter, a payslip, a report in colour and a near-blank sheet, in `src/Yass.Fake/Fixtures`.
`scripts/render-fixtures.sh` runs pandoc through typst to PDF and poppler to a greyscale PNG,
A4 at 150 dpi.

- **Rendered, not committed.** The Markdown is the source; the images go to `obj/` and are
  embedded from there. What a page says and what it looks like cannot drift apart, and a diff
  shows words rather than a binary blob. The cost is a toolchain: `pandoc`, `typst`,
  `poppler-utils` and a font, in the dev shell, the Nix package and the Docker build stage —
  the last of which adds about a minute to a cold `docker compose up --build`, and nothing to
  the runtime image.
- **typst rather than LaTeX** as the PDF engine: a single small binary against a distribution
  measured in gigabytes. It ships no fonts, so one has to be on `TYPST_FONT_PATHS` or it
  refuses to lay anything out — which is why DejaVu is in all three places too.
- **The fake writer embeds the pages.** Each is decoded, rotated and greyed as the edits ask,
  encoded as JPEG and put in the PDF whole as `/DCTDecode`. It needs ImageSharp, which the
  server and the exporter already depend on. Image exports are real images now as well.

## Consequences

- The demo shows real documents on the review screen and in the saved PDF, which was the point.
- A build without pandoc, typst, poppler and a font now fails rather than producing an
  assembly with no pages in it. That is the deliberate trade for never committing a generated
  file: the alternative was a script someone has to remember to run.
- Rendering is skipped for any document whose image is already newer than it, so the cost
  after the first build is nothing. MSBuild's own `Inputs`/`Outputs` are not used for this —
  its `Inputs` attribute takes a wildcard literally — so the script decides what is stale.
- The fixtures are 1241x1754 rather than 620x877, so the page-size assertions in
  `Yass.Export.Tests` read 150 dpi where they read 75. The demo README's apology for soft
  zoom is gone with them.
- Anything that wants a page of a particular shape — a very long document, a landscape one —
  is now a Markdown file away, where before it meant opening an image editor.
