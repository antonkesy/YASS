#!/bin/sh
# Renders assets/logo/yass.ico and yass.icns from the 512 px logo (outputs are committed).
set -eu
cd "$(dirname "$0")/../assets/logo"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

for size in 16 24 32 48 64 128 256 512; do
  magick yass-512.png -resize "${size}x${size}" -define png:exclude-chunks=date,time "$work/$size.png"
done

magick "$work/16.png" "$work/24.png" "$work/32.png" "$work/48.png" "$work/64.png" "$work/128.png" "$work/256.png" yass.ico
png2icns yass.icns "$work/16.png" "$work/32.png" "$work/48.png" "$work/128.png" "$work/256.png" "$work/512.png" >/dev/null
echo "wrote assets/logo/yass.ico and assets/logo/yass.icns"
