# ADR-011: `@wllama/wllama` runs a naming model on the processor

- **Status:** accepted
- **Date:** 2026-09-29
- **Amends:** ADR-010

## Context

ADR-010 put a naming model in the browser so that a payslip never has to be posted to
anyone. It runs on WebGPU and nothing else. Where WebGPU is missing — Firefox, Safari
until recently, a GPU the browser declines to use, a Linux box where it sits behind a
flag — settings showed a dead end that read "this browser has no WebGPU, so it cannot
run a model itself. Use an API key instead." That is the wrong sentence to hand the one
person who chose YASS *because* the documents should stay on their machine.

The hardware was never the obstacle. A 0.5B model quantised to four bits is under half a
gigabyte and decodes on any processor with that much spare RAM. What was missing was a
runtime that did not insist on a GPU.

## Decision

`@wllama/wllama`, pinned at `3.6.1`, as a second in-browser engine beside web-llm — not
instead of it. llama.cpp compiled to WebAssembly: it decodes on the processor and holds
the weights in ordinary RAM.

- **A second provider, not a setting inside the first.** `In-browser (GPU)` and
  `In-browser (CPU)` are two entries in the same list as Anthropic and OpenAI-style. They
  take different model catalogues in different formats and have different speeds, and
  `AiPrefs.models` is already keyed per provider, so each keeps the model you last chose
  for it.
- **In a worker, and only in the worker**, for the reasons ADR-010 gives. The plumbing
  that keeps that true — spawn, correlate replies by id, demux progress — is now shared
  by both engines in `worker.client.ts`, which imports neither package. The wasm is
  8.4 MB and reaches the page as an asset URL the worker fetches, never a page import.
- **The model list is written down here**, and this is where ADR-010 does not carry
  over. That ADR could say "the catalogue comes from the package, so no size or name can
  go stale"; wllama ships no catalogue. So `cpu-models.ts` holds four small instruct
  models with their sizes — and each URL is pinned to a **commit sha** rather than a
  branch, so the bytes behind it cannot change and the size beside it cannot go wrong.
  A free-text field takes any other GGUF URL, which is the escape hatch a fixed list
  needs.
- **As many threads as the machine will spare.** Multi-threaded wasm needs
  `SharedArrayBuffer`, which a browser only hands to a cross-origin isolated page, which
  means `Cross-Origin-Opener-Policy: same-origin` and
  `Cross-Origin-Embedder-Policy: require-corp` on what the host serves —
  `StaticFrontend.cs`, and the same from Vite in development so that the two do not differ.
  wllama then takes half the cores. `require-corp` rather than `credentialless` because
  every browser understands it, Safari included, and nothing is given up for it: everything
  this app loads from elsewhere — model weights, a naming API — it loads with `fetch` under
  CORS, which `require-corp` already accepts. Verified: the GPU engine's worker and its
  catalogue are unaffected.

Rejected: replacing web-llm with wllama, which does both backends from one GGUF
catalogue. It would be one dependency instead of two — but MLC compiles its own WebGPU
kernels and llama.cpp's WebGPU backend does not match them, so the people who *have* a
GPU would pay for the tidiness.

## Consequences

- The consequence ADR-010 listed — "WebGPU is required; where it is missing the option
  is disabled with that as its reason" — is retired. There is now an in-browser option
  for every browser, and the WebGPU message points at it.
- CPU naming is slower than GPU naming, and honestly so. Measured on a 16-core desktop with
  SmolLM2 135M Q8_0, first name after the download then five more: **13.1 s / 4.3 s** on one
  thread, **5.1 s / 0.9 s** on eight. Small enough for a file name, too slow for anything
  longer — which is the only thing being asked of it.
- Cross-origin isolation is checked when a worker is *created*, and the check reads the
  headers on the worker script. A copy cached from before isolation was turned on does not
  carry them, and `_app/immutable` is cached for a year without revalidating — so an upgrade
  across this change can leave a browser refusing to start a worker whose file never changed.
  Nothing has been released yet, so no one is in that position; what remains is that the
  browser's error event for it is empty, which `worker.client.ts` now answers by saying the
  worker never started and naming the cache as the likely cause.
- Two engines is two catalogues, two workers and two sets of weights in the cache. The
  shared plumbing keeps the duplication to the part that genuinely differs.
- The curated list will age. The sha pinning means it ages *visibly* — a stale entry is
  a stale entry, not a size that quietly stopped matching the file.
- Still one thing CI cannot run: hundreds of megabytes of weights per test. What CI
  covers is that the option appears without WebGPU, and that neither worker nor wasm is
  fetched on first paint.
