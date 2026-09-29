#!/bin/sh
# Renders the fake scanner's pages from the Markdown beside them: pandoc to PDF through typst,
# then poppler to a PNG at scan resolution. Called by Yass.Fake's build, which is why it takes
# its paths as arguments rather than guessing them.
#
#   render-fixtures.sh SOURCE_DIR OUTPUT_DIR [DPI]
#
# The default matches scanner.defaults.dpi, so the common case is a native-resolution page and
# the fake scanner has nothing to resample.
#
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

  # The build runs this every time, so skip what is already newer than its source. This is
  # where the incrementality lives: MSBuild's Inputs attribute takes a wildcard literally.
  if [ -f "$png" ] && [ "$png" -nt "$markdown" ] && [ "$png" -nt "$0" ]; then
    continue
  fi

  # A4 because that is what the scanner defaults to, and what the page size assertions in
  # Yass.Export.Tests read back out of the PDF. DejaVu because typst ships no fonts of its
  # own and an empty fallback list is a hard error.
  pandoc "$markdown" --pdf-engine=typst -V papersize=a4 -V mainfont="DejaVu Sans" -o "$pdf"

  # Greyscale, like a document scanner's own output, and the one thing tesseract most wants —
  # except the page whose name says it is in colour, which is there so the review screen's
  # colour handling has something to show.
  case "$name" in
    *-color) pdftoppm -png -r "$dpi" -singlefile "$pdf" "$output_dir/$name" ;;
    *) pdftoppm -png -gray -r "$dpi" -singlefile "$pdf" "$output_dir/$name" ;;
  esac
done
