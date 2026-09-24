#!/usr/bin/env bash
# Regenerates the TypeScript hub client from the C# contract in Yawss.Core.
# Output must be committed; `pnpm hub:check` fails CI when it drifts.
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${1:-src/lib/hub/generated}"
dotnet tool restore --tool-manifest ../.config/dotnet-tools.json > /dev/null
dotnet tsrts \
  --project ../src/Yawss.Core/Yawss.Core.csproj \
  --output "$OUT" \
  --serializer MessagePack \
  --naming-style None \
  --eol Lf > /dev/null
echo "Hub client generated into $OUT"
