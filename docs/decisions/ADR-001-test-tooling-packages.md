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

## Amended 2026-09-28: xUnit v3, and NSubstitute removed

`xunit` 2.9.3 is the last v2 release. Test projects move to
**`xunit.v3.mtp-off` 4.0.1** — the flavour of that release with Microsoft
Testing Platform disabled — so VSTest remains the runner and
`coverage.runsettings`, the CI `--settings`/`--logger trx` step and
`scripts/coverage-gate.py` keep working. The default `xunit.v3` package pulls
`xunit.v3.mtp-v2` instead and would have replaced the whole coverage pipeline;
MTP is a separate decision for a separate day.

v3 requires `<OutputType>Exe</OutputType>` on test projects and enforces it
with a build error. `IAsyncLifetime` now derives from `IAsyncDisposable` with
`ValueTask` members, which is also why the `CA1001` suppression is still
needed — CA1001 only accepts `IDisposable`.

`Microsoft.NET.Test.Sdk` → 18.10.1, `xunit.runner.visualstudio` → 4.0.0,
`coverlet.collector` → 10.1.0.

**`NSubstitute` and `NSubstitute.Analyzers.CSharp` are removed.** Nothing in
`tests/` ever used them: collaborators are faked with the real
`FakeScannerBackend` and with `NullLogger<T>.Instance`, which is the better
pattern. SPEC.md §3 no longer lists NSubstitute either.

coverlet 10.1.0 "relaxed auto-property skip logic and improved coverage for
records" (coverlet#1941), so it now counts record primary constructors that
`SkipAutoProps` used to hide. The reported total drops from 86.6 % to 80.5 %
against an 80 % gate. That is a more honest measurement, not a regression —
`Naps2ScannerBackend.cs` goes from 0/19 to 0/80 coverable lines, which is the
real size of an adapter that has no tests because it needs a scanner — but it
leaves almost no headroom, so the next uncovered addition will trip the gate.

**xUnit1051** (use `TestContext.Current.CancellationToken`) is suppressed in
`.editorconfig`. Adopting it is worthwhile and would make tests honour their
own timeout, but it is ~50 call sites of behaviour change.
