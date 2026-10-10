#!/usr/bin/env bash
# Usage: package-desktop.sh DIST_DIR  (AppImage, NSIS installer, macOS .app zip; ADR-016)
# appimagetool and the AppImage runtime are not in nixpkgs: fetched at pinned versions, SHA-256 checked.
set -euo pipefail

dist=$(cd "${1:?dist directory}" && pwd)
root=$(cd "$(dirname "$0")/.." && pwd)
version=${YASS_VERSION:-$(cat "$root/version.txt")}
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
APPIMAGETOOL_SHA256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
RUNTIME_URL=https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64
RUNTIME_SHA256=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d

fetch() { # url sha256 out
  curl -fsSL -o "$3" "$1"
  echo "$2  $3" | sha256sum -c --quiet -
}

published() { # rid -> folder publish-desktop.sh wrote
  local dir="$dist/yass-desktop-$version-$1"
  [ -x "$dir/Yass.Desktop" ] || [ -f "$dir/Yass.Desktop.exe" ] || { echo "missing $dir; run publish-desktop.sh first" >&2; exit 1; }
  echo "$dir"
}

appimage() {
  local app="$work/AppDir" out="$dist/YASS-$version-x86_64.AppImage"
  mkdir -p "$app/usr/lib"
  cp -a "$(published linux-x64)" "$app/usr/lib/yass"
  cp "$root/packaging/linux/AppRun" "$app/AppRun"
  cp "$root/packaging/linux/yass.desktop" "$app/yass.desktop"
  magick "$root/assets/logo/yass-512.png" -resize 256x256 "$app/yass.png"
  ln -s yass.png "$app/.DirIcon"

  fetch "$APPIMAGETOOL_URL" "$APPIMAGETOOL_SHA256" "$work/appimagetool"
  fetch "$RUNTIME_URL" "$RUNTIME_SHA256" "$work/runtime"
  chmod +x "$work/appimagetool"
  # Extract-and-run: CI runners and containers have no FUSE to mount the tool itself.
  ARCH=x86_64 VERSION="$version" "$work/appimagetool" --appimage-extract-and-run --no-appstream \
    --runtime-file "$work/runtime" "$app" "$out" >/dev/null
  echo "$out"
}

windows() {
  local out="$dist/YASS-$version-windows-x64-setup.exe"
  makensis -V2 -DVERSION="$version" -DSOURCE="$(published win-x64)" -DOUTFILE="$out" \
    "$root/packaging/windows/yass.nsi" >/dev/null
  echo "$out"
}

macos() {
  local stage="$work/mac" out="$dist/YASS-$version-macos-arm64.zip"
  local app="$stage/YASS.app"
  mkdir -p "$app/Contents/Resources"
  cp -a "$(published osx-arm64)" "$app/Contents/MacOS"
  cp "$root/assets/logo/yass.icns" "$app/Contents/Resources/yass.icns"
  sed "s/@VERSION@/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"
  printf 'APPL????' > "$app/Contents/PkgInfo"
  # Apple Silicon won't run an unsigned arm64 binary; ad hoc is enough. rcodesign warns about
  # every managed .dll, so its output goes to a log.
  rcodesign sign "$app" > "$work/rcodesign.log" 2>&1 || { cat "$work/rcodesign.log" >&2; exit 1; }
  # Zip, not .dmg: Linux can't write compressed images and an uncompressed one is ~3x the size.
  python3 - "$stage" "$out" <<'PY'
import os, sys, zipfile
stage, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, strict_timestamps=False) as z:
    for base, _, files in os.walk(stage):
        for f in files:
            path = os.path.join(base, f)
            info = zipfile.ZipInfo.from_file(path, os.path.relpath(path, stage), strict_timestamps=False)
            with open(path, "rb") as fh:
                z.writestr(info, fh.read(), zipfile.ZIP_DEFLATED)
PY
  echo "$out"
}

appimage
windows
macos
