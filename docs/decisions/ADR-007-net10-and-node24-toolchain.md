# ADR-007: Target .NET 10 LTS and Node 24 LTS

- **Status:** accepted
- **Date:** 2026-09-28

## Context

SPEC.md §3 records **.NET 8 LTS** as DECIDED. .NET 8 reaches end of life on
2026-11-10, so that row has an expiry date and needs replacing rather than
reinterpreting.

## Decision

Target **net10.0** on SDK 10.0.401, with the ASP.NET Core 10.0.12 runtime, and
move the dev shell and container web stage to **Node 24 LTS** (nixpkgs
`nodejs_24` = 24.20.0).

.NET 10 is the current LTS, supported to 2028-11-14. .NET 11 was rejected: it
is still RC, nixpkgs has no `sdk_11_0`, and no dependency here ships a net11.0
target. Node 26 was rejected because it is not LTS until October; Node 24
satisfies every `engines` constraint in the web tree, including jsdom 30's
`^24.15.0`.

## Consequences

`AnalysisLevel=latest-all` with `TreatWarningsAsErrors=true` means each SDK
brings its new analyzer wave in as build errors. Two rules from the .NET 9/10
waves are suppressed in `.editorconfig` as policy, not deferred work:

- **CA1515** (make public types internal) — `Yass.Server` is an executable, but
  `Yass.Server.Tests` references its types and `WebApplicationFactory<Program>`
  requires `Program` to stay public.
- **CA1873** (expensive logging arguments) — the flagged arguments are property
  reads and counts. Same reasoning as the existing CA1848 suppression.

One rule was fixed rather than suppressed: CA1875, `Regex.Matches(..).Count` →
`Regex.Count`.

Pinning `AnalysisLevel` to `10.0-all` is the escape hatch if a future SDK's
wave is ever too large to absorb in one commit.

Two dependencies are deliberately held back, and both are checkable:

- **TypeScript stays on 6.x.** 7.0.2 exists, but `svelte-check` 4.7.6 peers
  `^5 || ^6`, `@sveltejs/kit` 2.70.3 peers `^5.3.3 || ^6.0.0`, and
  `typescript-eslint` 8.71.0 peers `>=4.8.4 <6.1.0`. Revisit when those move.
- **NAPS2.Sdk stays on 1.2.1.** See ADR-004.

`nix/deps.json` shrinks from 118 to 35 entries because .NET 10 prunes the
`System.*` and `runtime.*` shim packages the shared framework now provides.
