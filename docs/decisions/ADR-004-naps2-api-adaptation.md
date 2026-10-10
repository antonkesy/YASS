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

## Amended 2026-09-28: staying on 1.2.1 under .NET 10

NAPS2.Sdk **1.3.0** is available and adds a net10.0 target, but that target's
assembly is annotated `[RequiresPreviewFeatures]` — its net8.0 and net462
assemblies are not. Using it raises 163 CA2252 warnings which, because the
attribute is transitive, spread to six projects including `Yass.Core` and
`Yass.Server`; clearing them means `EnablePreviewFeatures=true` (and
`LangVersion=preview`) across all of them.

**Decision: stay on 1.2.1.** It builds clean and passes the full suite on
net10.0, and upstream marking only its .NET 10 build as preview is a statement
about that build's stability, not a formality worth suppressing in the scan
path. The public-surface findings above were re-checked and are unchanged.
SPEC.md §3 therefore still reads "NAPS2.Sdk (NuGet, 1.2.x)".

Revisit when 1.3.x drops the preview annotation.

## Amended 2026-10-09: drivers on Windows and macOS (desktop app)

The desktop app (ADR-014) runs on Windows and macOS as well as Linux. NAPS2's per-platform
default driver is WIA on Windows, Apple ImageCaptureCore on macOS and SANE on Linux.

- **Windows:** `Driver.Default` is WIA and works from the plain `net8.0` assembly, through
  `NAPS2.Wia`, which is already a dependency. `scanner.driver` gains `wia` for choosing it
  explicitly. TWAIN is left out: it needs the 32-bit `NAPS2.Sdk.Worker.Win32` process.
- **macOS:** the Apple driver exists only in a `net8-macos` build of the SDK. The 1.2.1 NuGet
  package does not ship one; its own error reads "Make sure you're using the right framework
  target (e.g. net8-macos for the Apple driver)". `NAPS2.Images.Mac` needs that target too.
  Building the desktop app as a macOS-workload app to get it is out of proportion, so on macOS
  **`auto` means eSCL**: network scanners, and USB ones that speak eSCL. `scanner.driver` gets
  no `apple` value, because it could only ever fail. SANE on macOS (through
  `NAPS2.Sane.Binaries`) would be a new dependency; it is left for when someone needs it.

## Amended 2026-10-09: cameras are not scanners

SANE and WIA both list cameras beside scanners, and NAPS2 hands them over as ordinary
`ScanDevice`s. YASS never offers them:

- **SANE:** the id is `backend:address`. A device from a camera backend (`v4l`, `gphoto2`,
  `dc210`, `dc240`, `dc25`, `dmc`, `qcam`, `stv680`) is dropped at discovery.
- **WIA:** the id says nothing about the device, so discovery asks WIA itself for each device's
  `DIP_DEV_TYPE`. Digital cameras and streaming video are dropped. This uses **`NAPS2.Wia`**
  2.0.3, the wrapper NAPS2.Sdk already depends on and is pinned to, now referenced directly;
  it adds no new package. If WIA cannot be asked, nothing is filtered, so a failed lookup never
  leaves the list empty.

## Amended 2026-10-10: file-backed export, and NAPS2's progress handler

Point 2's "embedded verbatim" did not hold: the export's `ScanningContext` had no
`FileStorageManager`, so `ImageImporter` decoded every rendered page into memory (about 26 MB a
colour A4 page at 300 dpi, all held until the PDF was written) and `PdfExporter` re-encoded them.
Each export now uses a context whose `FileStorageManager` is its own temp folder: pages stay
files, and a JPEG goes into the PDF as rendered, at `output.pdf.jpeg_quality`.

Point 3 is out of date: `NAPS2.Util.ProgressHandler` (in NAPS2.Internals, already a transitive
dependency) is usable. `PdfExporter.Export` gets one carrying the caller's cancellation, so a
cancelled export stops NAPS2 instead of abandoning it mid-write, and its reports fill the last
10 % of the progress bar.
