# ADR-004: Adapting to the NAPS2.Sdk 1.2 public surface

- **Status:** accepted
- **Date:** 2026-09-24

## Context

SPEC.md assumes NAPS2 can (a) build a PDF from our on-disk PNGs with edits
applied, (b) flag blank pages, and (c) deskew. Inspecting NAPS2.Sdk 1.2.1:

- `ScanningContext.CreateProcessedImage` and `BlankDetectionImageOp` are
  `internal`; the only public way to obtain a `ProcessedImage` from a file is
  `NAPS2.ImportExport.ImageImporter`.
- `PdfExporter` embeds JPEG or lossless data based on the imported image's
  metadata and re-encodes transformed JPEGs at its own default quality.
- Blank pages and deskew are only available at scan time through
  `ScanOptions.ExcludeBlankPages` / `AutoDeskew`.

## Decision

1. **Scanning:** map our options onto `NAPS2.Scan.ScanOptions`; let NAPS2
   drop blank pages (`ExcludeBlankPages`) and deskew (`AutoDeskew`) during the
   scan. Real scans therefore never produce `IsBlank = true` pages; the flag
   remains for the fake backend and for any future detector.
2. **Export:** each active page is rendered once with ImageSharp (rotation,
   colour mode, DPI metadata) into a temp JPEG at `output.pdf.jpeg_quality`
   (or PNG when `compression: lossless`), imported with `ImageImporter`, and
   assembled with NAPS2's `PdfExporter`. An untransformed JPEG is embedded
   verbatim, so the configured quality is what ends up in the PDF.
3. Progress is reported per page by our code, since NAPS2's progress
   delegate type is not part of the public surface we compile against.

## Consequences

The domain model is unchanged. Blank-page threshold semantics follow NAPS2:
`blank_page_threshold` (0..1) is scaled to NAPS2's 0..100 coverage threshold.
If a later SDK exposes `CreateProcessedImage`, step 2 can drop the temp file.
