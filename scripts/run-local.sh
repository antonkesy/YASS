#!/usr/bin/env bash
# Usage: scripts/run-local.sh [config.yaml] [-- extra server args, e.g. --fake]
set -euo pipefail
cd "$(dirname "$0")/.."
config="$(realpath "${1:-yass.yaml}")"; shift || true # absolute: dotnet run changes the working directory
[ "${1:-}" = "--" ] && shift
if ! command -v dotnet >/dev/null || [[ "$(dotnet --version)" != 10.* ]]; then
  exec nix develop -c "$0" "$config" -- "$@"
fi
[ -f web/build/index.html ] || pnpm --dir web install --frozen-lockfile
pnpm --dir web build
YASS_WEBROOT="$PWD/web/build" exec dotnet run --project src/Yass.Server -- --config "$config" "$@"
