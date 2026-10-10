#!/bin/sh
# Usage: render-fixtures.sh SOURCE_DIR OUTPUT_DIR [DPI]  (called by Yass.Fake's build)
# POSIX sh, not bash: this also runs in the Alpine SDK image, which has no bash.
set -eu

source_dir=${1:?source directory}
output_dir=${2:?output directory}
dpi=${3:-300}

mkdir -p "$output_dir"

for markdown in "$source_dir"/*.md; do
  name=$(basename "$markdown" .md)
  pdf="$output_dir/$name.pdf"
  png="$output_dir/$name.png"

  # Skip up-to-date outputs: MSBuild's Inputs takes the wildcard literally, so incrementality lives here.
  if [ -f "$png" ] && [ "$png" -nt "$markdown" ] && [ "$png" -nt "$0" ]; then
    continue
  fi

  # A4 is what the tests read back; typst ships no fonts and errors on an empty fallback, hence DejaVu.
  # Landscape goes via the preamble: pandoc's template calls conf(), which overrides in-body set rules.
  case "$name" in
    *-landscape)
      flip=$(mktemp)
      printf '#set page(flipped: true)\n' > "$flip"
      pandoc "$markdown" --pdf-engine=typst -H "$flip" -V papersize=a4 -V mainfont="DejaVu Sans" -o "$pdf"
      rm -f "$flip"
      ;;
    *)
      pandoc "$markdown" --pdf-engine=typst -V papersize=a4 -V mainfont="DejaVu Sans" -o "$pdf"
      ;;
  esac

  # Greyscale like a real scanner, except *-color, which exercises the review screen's colour handling.
  case "$name" in
    *-color) pdftoppm -png -r "$dpi" -singlefile "$pdf" "$output_dir/$name" ;;
    *) pdftoppm -png -gray -r "$dpi" -singlefile "$pdf" "$output_dir/$name" ;;
  esac
done
