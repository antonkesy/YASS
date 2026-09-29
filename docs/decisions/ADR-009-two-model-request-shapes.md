# ADR-009: The browser speaks two model request shapes

- **Status:** accepted
- **Date:** 2026-09-29

## Context

ADR-008 has the browser calling a model API itself. Which ones? A home user already runs
something — LM Studio on a desktop, llama.cpp on a spare box, an OpenRouter account, a key
from whoever — and the feature is worth little if it only talks to the one provider we chose.

Calling an API from a page is not the same as calling it from a server: it needs the host to
allow cross-origin requests, which is per-provider and not a thing a plan can assume.

## Decision

Two request shapes, and a base URL the user can point anywhere.

- **Anthropic** — `POST {base}/v1/messages`, `x-api-key`, `anthropic-version: 2023-06-01`,
  and `anthropic-dangerous-direct-browser-access: true`, which is what the official SDK sends
  for `dangerouslyAllowBrowser`.
- **OpenAI-compatible** — `POST {base}/chat/completions`, `authorization: Bearer`. This is
  the shape OpenAI, Groq, OpenRouter, LM Studio, llama.cpp and Ollama all speak, so one
  implementation plus a base-URL field reaches all of them.

Both were verified from a page served over plain http, with a deliberately invalid key, by
watching whether the reply was readable at all:

| Host | Result |
|---|---|
| `api.anthropic.com` **with** the browser-access header | `401 invalid x-api-key` — allowed |
| `api.anthropic.com` **without** it | `TypeError: Failed to fetch` — blocked |
| `api.openai.com` | `401` — allowed |
| `openrouter.ai` | `401` — allowed |
| `api.groq.com` | `401` — allowed |

A translation layer over a single shape was rejected: the second shape is about thirty lines,
less than an abstraction that turns one into the other would be.

## Consequences

- A cross-origin refusal is indistinguishable from a dead network in JavaScript — both are a
  bare `TypeError` with no status — so the error message has to name both causes. The
  settings sheet has a Test button that runs the real path, so this is found deliberately
  there rather than at a scanner with a document in hand.
- Ollama refuses cross-origin requests until `OLLAMA_ORIGINS` names the YASS origin. That is
  a documentation problem, not a code one.
- YASS is served over http, so it may call https endpoints freely. Behind TLS the reverse
  would bite: an https page may not call `http://192.168.x.x:11434`, though loopback stays
  exempt.
- Two answer shapes to parse, both one property deep, both pinned by unit tests against an
  injected `fetch`.
