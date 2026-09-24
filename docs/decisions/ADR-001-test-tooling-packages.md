# ADR-001: Test tooling packages

- **Status:** accepted
- **Date:** 2026-09-24

## Context

SPEC.md names xUnit, NSubstitute and `WebApplicationFactory` for backend
tests, and an 80 % line coverage gate. Running xUnit under `dotnet test`
and collecting coverage needs packages the spec does not list explicitly.

## Decision

Add, for test projects only, via central package management:

- `Microsoft.NET.Test.Sdk` — required host for `dotnet test`.
- `xunit.runner.visualstudio` — the xUnit VSTest adapter.
- `coverlet.collector` — line coverage for the CI gate; no code changes,
  enabled with `--collect:"XPlat Code Coverage"`.
- `NSubstitute.Analyzers.CSharp` — catches misuse of NSubstitute at build
  time, consistent with `TreatWarningsAsErrors`.

## Consequences

Test projects share one package set from `Directory.Build.props`; coverage
tooling is in place before the CI commit needs it.
