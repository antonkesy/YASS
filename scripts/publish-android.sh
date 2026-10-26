#!/bin/sh
# Usage: publish-android.sh OUTPUT_DIR [RID...]  (default: android-arm64 android-x64; needs web/build)
# Run inside `nix develop .#android`. Writes OUTPUT_DIR/yass-VERSION-RID.apk, one ABI each (SPEC.md §7).
set -eu

out=${1:?output directory}
shift
[ "$#" -gt 0 ] || set -- android-arm64 android-x64

root=$(cd "$(dirname "$0")/.." && pwd)
[ -f "$root/web/build/index.html" ] || { echo "web/build is missing; run pnpm --dir web build" >&2; exit 1; }
mkdir -p "$out"
out=$(cd "$out" && pwd)
# The release tag's version when there is one (an alpha is not in version.txt); the
# integer versionCode always comes from version.txt.
version=${YASS_VERSION:-$(cat "$root/version.txt")}

# Installs into ~/.dotnet. The install can drop its own packs after updating its manifests; repair puts them back.
if ! dotnet workload list | grep -q '^android '; then
  dotnet workload install android
  dotnet workload repair
fi

for rid in "$@"; do
  dir=$(mktemp -d)
  dotnet publish "$root/src/Yass.Android/Yass.Android.csproj" -c Release -r "$rid" \
    -p:ApplicationDisplayVersion="$version" -o "$dir"
  cp "$dir/io.github.antonkesy.yass-Signed.apk" "$out/yass-$version-$rid.apk"
  rm -rf "$dir"
done
