#!/usr/bin/env bash
# Fails when the committed generated hub client differs from what the C# contract produces now.
set -euo pipefail
cd "$(dirname "$0")/.."
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
./scripts/hub-generate.sh "$TMP/generated" > /dev/null
if ! diff -r "$TMP/generated" src/lib/hub/generated; then
  echo "Hub contract drift: run 'pnpm hub:generate' in web/ and commit the result." >&2
  exit 1
fi
echo "Hub contract matches Yawss.Core."
