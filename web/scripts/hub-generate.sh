#!/usr/bin/env bash
# Regenerates the TypeScript hub client from the C# contract in Yass.Core.
# Output must be committed; `pnpm hub:check` fails CI when it drifts.
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${1:-src/lib/hub/generated}"
dotnet tool restore --tool-manifest ../.config/dotnet-tools.json > /dev/null
# tsrts prints "can not be transpiled" and still exits 0, so a silent redirect would
# happily report success on a contract that is missing types.
LOG=$(dotnet tsrts \
  --project ../src/Yass.Core/Yass.Core.csproj \
  --output "$OUT" \
  --serializer MessagePack \
  --naming-style None \
  --eol Lf 2>&1)
if grep -qE 'can not be transpiled|is not supported' <<< "$LOG"; then
  echo "$LOG" >&2
  echo "hub generation failed: a type could not be transpiled (see above)" >&2
  exit 1
fi
# The generator ends files with a blank line; pre-commit's end-of-file-fixer strips it. Normalise so the
# committed output is byte-identical to a fresh generation.
find "$OUT" -name '*.ts' -exec sed -i -e :a -e '/^\n*$/{$d;N;ba' -e '}' {} +
echo "Hub client generated into $OUT"
