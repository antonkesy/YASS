# Shortcuts. Recipes run from the repo root whatever directory you call them from, and enter
# the dev shell themselves when not already in it.

set shell := ["bash", "-euo", "pipefail", "-c"]

# Inside `nix develop`, or the dev shell's toolchain on PATH: run as is. Otherwise wrap in it.
dev := if env("IN_NIX_SHELL", "") != "" { "" } else { "nix develop -c" }

# The desktop app against the simulated scanner. Pass a scenario, or "" for a real scanner.
desktop scenario="pages=3,blank=2": web
    YASS_WEBROOT="$PWD/web/build" {{ dev }} with-desktop-libs dotnet run --project src/Yass.Desktop -- {{ if scenario == "" { "" } else { "--fake '" + scenario + "'" } }}

# The server against the simulated scanner, on http://localhost:8080.
server scenario="pages=3,blank=2": web
    YASS_WEBROOT="$PWD/web/build" {{ dev }} dotnet run --project src/Yass.Server -- {{ if scenario == "" { "" } else { "--fake '" + scenario + "'" } }}

# Build the web app (installs its packages the first time).
web:
    [ -d web/node_modules ] || {{ dev }} pnpm --dir web install --frozen-lockfile
    {{ dev }} pnpm --dir web build

# The commit gate from AGENTS.md.
check:
    {{ dev }} pre-commit run --all-files
    {{ dev }} dotnet test
    {{ dev }} pnpm --dir web test
