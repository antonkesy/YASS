#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${1:-src/lib/hub/generated}"
dotnet tool restore --tool-manifest ../.config/dotnet-tools.json > /dev/null
# tsrts prints "can not be transpiled" yet exits 0, so grep its output for failures.
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
# Strip the generator's trailing blank line, as end-of-file-fixer would, so output is byte-identical.
find "$OUT" -name '*.ts' -exec sed -i -e :a -e '/^\n*$/{$d;N;ba' -e '}' {} +
echo "Hub client generated into $OUT"
