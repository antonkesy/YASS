#!/bin/sh
# Publishes the desktop app for each runtime, self-contained, with the web build beside it, and
# zips each one. Everything cross-compiles from Linux: Photino's native parts come prebuilt
# from NuGet, so one machine (and the fixture toolchain Yass.Fake needs) covers all three.
#
#   publish-desktop.sh OUTPUT_DIR [RID...]     (default: linux-x64 win-x64 osx-arm64)
#
# Expects web/build to exist (pnpm --dir web build), and the fixture toolchain Yass.Fake builds
# with (ADR-012). Run it with Microsoft's own SDK, as CI does in the stock Alpine SDK image: the
# Nix SDK's Linux apphost and runtime are patched to load from /nix/store, and would not start
# on any other distribution. (`nix build .#desktop` is the Nix way to the same app.)
set -eu

out=${1:?output directory}
shift
[ "$#" -gt 0 ] || set -- linux-x64 win-x64 osx-arm64

root=$(cd "$(dirname "$0")/.." && pwd)
[ -f "$root/web/build/index.html" ] || { echo "web/build is missing; run pnpm --dir web build" >&2; exit 1; }
mkdir -p "$out"
out=$(cd "$out" && pwd)
version=$(cat "$root/version.txt")

for rid in "$@"; do
  case "$rid" in
    linux-*)
      if command -v dotnet | grep -q '^/nix/store/'; then
        echo "refusing to publish $rid with the Nix SDK: it would only run on Nix (see above)" >&2
        exit 1
      fi
      ;;
  esac
  dir="$out/yass-desktop-$version-$rid"
  rm -rf "$dir"
  dotnet publish "$root/src/Yass.Desktop/Yass.Desktop.csproj" -c Release -r "$rid" --self-contained -o "$dir"
  cp -r "$root/web/build" "$dir/wwwroot"
  cp "$root/yass.example.yaml" "$dir/"
  # Files that came out of the Nix store are dated 1970, which plain zip tools refuse.
  python3 - "$dir" <<'PY'
import os, sys, zipfile
src = sys.argv[1]
with zipfile.ZipFile(src + ".zip", "w", zipfile.ZIP_DEFLATED, strict_timestamps=False) as z:
    for base, _, files in os.walk(src):
        for f in files:
            path = os.path.join(base, f)
            info = zipfile.ZipInfo.from_file(path, os.path.relpath(path, os.path.dirname(src)), strict_timestamps=False)
            with open(path, "rb") as fh:
                z.writestr(info, fh.read(), zipfile.ZIP_DEFLATED)
PY
  echo "$dir.zip"
done
