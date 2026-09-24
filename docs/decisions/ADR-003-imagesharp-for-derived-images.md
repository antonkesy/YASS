# ADR-003: ImageSharp renders thumbnails and previews

- **Status:** accepted
- **Date:** 2026-09-24

## Context

The review UI needs WebP thumbnails and previews with rotation and colour
mode applied. SPEC.md lists ImageSharp as an allowed existing tool and NAPS2
already depends on `SixLabors.ImageSharp` through `NAPS2.Images.ImageSharp`.

## Decision

`Yawss.Server` references `SixLabors.ImageSharp` directly (same major as
NAPS2's dependency) and renders derived images itself: load PNG, rotate,
grayscale/threshold, resize, encode WebP. Decoded bitmaps are bounded by a
semaphore sized from `processing.max_concurrent_renders`.

Rejected: going through NAPS2's `ImageContext` for thumbnails, which would
pull the scanning SDK into the server before `Yawss.Scanning` exists and adds
nothing over the ImageSharp calls.

## Consequences

Thumbnail format is WebP (SPEC §14 open item resolved: encode cost is
negligible at 320 px). ImageSharp's pixel-format defaults apply; no GTK or
native dependencies.
