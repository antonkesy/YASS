# ADR-008: A naming model can be set up in the browser, key and all

- **Status:** accepted
- **Date:** 2026-09-29

## Context

ADR-006 put the name-suggestion model in `yass.yaml` with the API key in a file on the
server, for a good reason: YASS has no authentication, so a key in the config or the
environment is a key anyone who can reach the port can read back.

That leaves the feature out of reach of the person actually using it. Setting up a model
means shelling into the server, editing YAML and writing a key file — while the UI's own
settings sheet, which already says "Stored in this browser only", sits one click away.

## Decision

A second, independent path: the browser holds the model settings and calls the model API
itself. `yass.yaml`, the server-side provider and `naming.api_key_file` are unchanged;
this is additive.

- The key lives in `localStorage` under `yass.ai.keys`, in its own blob so that spreading
  the preferences object can never leak it into a prop, a log or an export.
- The page calls the provider directly. **The key never reaches the server**, so ADR-006's
  rule stands untouched: nothing YASS stores server-side holds a secret.
- The server still reads the page. `SuggestNames` now returns the OCR text alongside its
  suggestions, so the browser's model works on the same input. Where the server cannot read
  text — `naming.ocr: false`, or no `tesseract` — the browser sends the page image instead,
  which it already has.
- **A server-side proxy is rejected.** It is the obvious workaround for the CORS risk and it
  would undo the whole point: the key would be back on an unauthenticated box, and YASS
  would become an open relay to a paid API for anyone on the LAN.

The privacy line in the popover is now computed from what the *browser* did as well as what
the server did. A server with `provider: off` no longer gets to claim "nothing leaves the
scanner server" while the page is posting to `api.anthropic.com`.

## Consequences

- `localStorage` is readable by any script on the origin and survives until it is cleared.
  On a personal laptop that is fine; on a shared browser beside the scanner it is an
  exposure, and the settings sheet says so rather than leaving it to this file.
- Two places can now name a document, and they can disagree. They are ordered — the server's
  suggestions render first and the browser's answer is merged in on top — so a slow, broken
  or unpaid model never costs you the heading and template suggestions that already worked.
- Each browser is configured separately. For one person with one laptop that is the point;
  for a household it is repeated setup.
- It works in a later Electron build unchanged, which a server-side key would not.
