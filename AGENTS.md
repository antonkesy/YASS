# YAWSS
Read SPEC.md before doing anything. It is the source of truth.
- Follow the commit plan in SPEC.md §11 in order. One logical change per commit, Conventional Commit messages.
- Every commit must pass `pre-commit run --all-files`, `dotnet test`, `pnpm test`.
- Do not add dependencies without an ADR in docs/decisions/.
- Ask before changing the config schema, adding a database, or adding auth.
- Use `nix develop` for the toolchain.
