# ADR-005: Read page text with NAPS2's Tesseract engine

- Status: accepted
- Date: 2026-09-28

## Context

The UI suggests a file name from what is printed on the page, which means reading text off a
scan. SPEC.md §3 lists OCR as not-in-v1 on the grounds that paperless-ngx does it. That still
holds for the archive: YASS does not produce a searchable PDF and does not store extracted
text. What it needs is a few lines from the top of page one, once, to propose a name.

## Decision

Use `NAPS2.Ocr.TesseractOcrEngine`, already present in NAPS2.Sdk, rather than adding a
Tesseract binding of our own. It shells out to the `tesseract` binary, so the binary becomes a
runtime dependency, added to the Nix dev shell, the Nix docker image and the Dockerfile.

Off by default (`naming.ocr: false`). When the binary is missing the reader reports itself
unavailable and the UI hides the Find name button; nothing fails.

We read `OcrResult.Lines`, which carries a font size per line. The largest line in the top
third of the first page is a title far more often than not, and the ratio of that size to the
page's median line size is a usable confidence signal — without a model in the loop at all.

## Consequences

- No new NuGet package; SPEC.md §2 ("use existing tools") is satisfied.
- One more runtime dependency, and packaging has to carry it.
- Text is read for naming only. It is not stored, not indexed, and not put in the PDF, so
  paperless-ngx remains the thing that makes an archive searchable.
- Accuracy is Tesseract's. The suggestion is a proposal the user accepts or ignores, so a poor
  read costs a click rather than a wrong file.

## Amended 2026-09-29: optional for the AI suggestion

ADR-008 lets a model configured in the browser name a document. Where this reader is
unavailable — `naming.ocr: false`, or no binary — that model is sent the page image instead, so
`tesseract` is no longer required for a suggestion to appear at all. It is still required for
the heading suggestion, which is the one that needs no model, and for a model running inside
the browser, which cannot see images.
