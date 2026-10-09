# ADR-013: Inter as the UI typeface, after Apple's own

- **Status:** accepted
- **Date:** 2026-10-09

## Context

The UI is restyled after Apple's apps: system blue and greys, lighter weights, flat
buttons. Manrope, the face it shipped with, is geometric and wide, and works against
that look more than any colour does.

## Decision

`--font-sans` asks for the platform's own face first (`-apple-system`,
`BlinkMacSystemFont`: SF on Apple devices) and falls back to **Inter** everywhere else.
Inter is self-hosted like Manrope was: the variable woff2 files, Latin and Latin
Extended, from `@fontsource-variable/inter` 5.3.0 (OFL-1.1), copied into
`web/static/fonts/`. It is not an npm dependency, so nothing changes in the lockfile.
A home server may have no internet, so a font service is not an option.

Considered: SF Pro itself, whose licence does not allow serving it to other platforms;
`system-ui` alone, which lands on Cantarell, Noto or Segoe depending on the box and makes
the app look different on each.

## Consequences

About 130 kB of fonts instead of 40 kB, cached after the first visit. Apple devices
download nothing extra, since they never fall through to Inter. Updating Inter means
copying the two files again.
