# ADR-010: `@mlc-ai/web-llm` runs a naming model in the browser

- **Status:** accepted
- **Date:** 2026-09-29

## Context

ADR-008 lets this browser call a model API with a key the user pastes in. That covers the
person who has an account somewhere. It does not cover the one whose whole reason for running
a scanner on their own hardware is that payslips and medical letters should not be posted to
anyone — for whom the honest options were a local Ollama (a second service to install and
keep running) or nothing.

WebGPU has made a third option real: the model runs in the page.

## Decision

`@mlc-ai/web-llm`, pinned at `0.2.85`, as a runtime dependency of the web app — the third one
it has ever had, after the two SignalR packages.

- **In a worker, and only in the worker.** Decoding on the main thread would freeze the review
  screen for seconds, on the one screen where someone is flipping through pages — but that is
  the smaller reason. The package is six megabytes, and a dynamic `import()` of it from the
  page still produces a six-megabyte page chunk. So the page holds no reference to it at all:
  the worker owns it, and the two speak a message protocol of ours instead of through
  `WebWorkerMLCEngine`, whose client half lives on the page and would bring the package with
  it. Measured: 0 bytes of it are fetched on first paint, and the page's own JavaScript grows
  by 2 kB.
- **Not precached.** The service worker's glob skips `workers/**`; without that, every visitor
  would download the runtime on their first visit, and `vite-plugin-pwa` fails the build
  rather than let it silently happen.
- **The model list comes from the package**, not from us: `prebuiltAppConfig.model_list`
  carries the ids and each entry's own `vram_required_MB`, so no size or name is written down
  here to go stale.
- **The weights are downloaded once, from a CDN, on an explicit click in settings** — never
  from the review screen with a document in hand. After that they live in the browser's cache
  and it works offline.

Rejected: shipping a model with the server. SPEC §9 caps the container at 200 MB and the
smallest useful weights are larger than that.

## Consequences

- A LAN-only product now has a feature whose first use needs the internet. It is opt-in,
  it says so before it downloads anything, and it is the only way to get a model without
  either a third party or a second service.
- The cache is per-origin per-browser, so a laptop and a phone each download their own copy.
- WebGPU is required. Where it is missing the option is disabled with that as its reason,
  rather than failing when it is asked to work.
- The happy path cannot be tested in CI: headless Chromium has no WebGPU, and the download is
  hundreds of megabytes. What CI covers is that the option disables itself without WebGPU and
  that the import stays out of the entry bundle; the rest is verified by hand.
- An Electron build could point `model_list` at weights shipped alongside it and drop the
  one-time download entirely. That is the reason this belongs in the browser rather than on
  the server.
