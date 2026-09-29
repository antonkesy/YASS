# ADR-006: Name suggestions go through a pluggable provider, off by default

- Status: accepted
- Date: 2026-09-28

## Context

Turning a page of OCR text into a short, sensible file name is what a language model is good
at. YASS is a LAN-only service with no authentication that a user runs on their own hardware,
and its documents are things like payslips and medical letters. Sending those to a third party
by default would be indefensible; refusing to ever do it would rule out the better result for
users who want it.

## Decision

A `naming:` config section with `provider: off | ollama | anthropic`, defaulting to `off`.

- `off` — no model is called. Suggestions come from the page text and the filename template.
- `ollama` — a model on the same machine (or LAN). Page text does not leave the network.
- `anthropic` — the API, with the key read from a file named by `api_key_file`, never from
  the config or the environment.

The UI states which of these is in force: the popover footer reads "Nothing leaves the scanner
server" only under `off` or `ollama`, and says the text is sent onward under `anthropic`.

Every failure — no provider, no network, a timeout, a malformed reply — degrades to the
suggestions that did work. Naming never fails a scan.

## Consequences

- Two HTTP shapes to maintain. Both are small and pinned to one endpoint each.
- The privacy claim in the UI is derived from config rather than hardcoded, so it cannot drift
  from what the server actually does.
- Adding a provider is a new branch plus a config value, not a new abstraction layer.

## Amended 2026-09-29: extended by ADR-008

The browser may now hold its own model settings and call a provider directly. Everything
above is unchanged — the config section, the file-only key, the degrade-never-fail rule — and
the browser path is additive. One line does change: the "nothing leaves the scanner server"
claim is now derived from what the browser did as well as what the server did, because a
server with `provider: off` no longer has the last word on where the text went.
