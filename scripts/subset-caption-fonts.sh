#!/usr/bin/env bash
# Regenerates the Latin-subset WOFF2 caption fonts the page previews with, from the TTFs the
# render worker uses. Needs fonttools and brotli (pip install fonttools brotli).
set -euo pipefail
cd "$(dirname "$0")/../frontend/public/fonts"
LATIN="U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+2000-206F,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD"
# Abril Fatface is excluded: its OFL has Reserved Font Names, which a subset may not carry.
for font in Poppins-ExtraBold.ttf SpaceMono-Bold.ttf; do
  pyftsubset "$font" --unicodes="$LATIN" --flavor=woff2 --layout-features='*' \
    --output-file="${font%.ttf}.latin.woff2"
done
