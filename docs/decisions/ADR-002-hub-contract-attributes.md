# ADR-002: TypedSignalR attributes in Yawss.Core

- **Status:** accepted
- **Date:** 2026-09-24

## Context

SPEC.md wants the hub contract defined once in C# and the TypeScript client
generated with `TypedSignalR.Client.TypeScript`. The generator only picks up
interfaces marked `[Hub]`/`[Receiver]` and types marked
`[TranspilationSource]`, and those attributes must live in the assembly that
defines the contract, which is `Yawss.Core`.

## Decision

Reference `TypedSignalR.Client.TypeScript.Attributes` (which pulls in
`Tapper.Attributes` and `TypedSignalR.Client.Attributes`) from `Yawss.Core`.
All three are attribute-only, netstandard2.0 assemblies with no runtime
behaviour, so the "BCL only" rule for Core keeps its intent: no logic, no
framework coupling. The generator itself is a dotnet tool restored in
`web/` tooling, not a project dependency.

## Consequences

Enum members and DTOs carry `[TranspilationSource]`; forgetting one fails the
contract check in CI rather than at runtime.
