#!/bin/sh
# Renders assets/logo/yass.ico, yass.icns and the Android launcher icons from the 512 px logo (outputs are committed).
set -eu
cd "$(dirname "$0")/../assets/logo"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

for size in 16 24 32 48 64 128 256 512; do
  magick yass-512.png -resize "${size}x${size}" -define png:exclude-chunks=date,time "$work/$size.png"
done

magick "$work/16.png" "$work/24.png" "$work/32.png" "$work/48.png" "$work/64.png" "$work/128.png" "$work/256.png" yass.ico
png2icns yass.icns "$work/16.png" "$work/32.png" "$work/48.png" "$work/128.png" "$work/256.png" "$work/512.png" >/dev/null
# Android launcher icons, 48 dp at each density.
res=../../src/Yass.Android/Resources
for density in mdpi:48 hdpi:72 xhdpi:96 xxhdpi:144 xxxhdpi:192; do
  mkdir -p "$res/mipmap-${density%%:*}"
  magick yass-512.png -resize "${density#*:}x${density#*:}" -define png:exclude-chunks=date,time "$res/mipmap-${density%%:*}/ic_launcher.png"
done
echo "wrote assets/logo/yass.ico, assets/logo/yass.icns and the Android launcher icons"
